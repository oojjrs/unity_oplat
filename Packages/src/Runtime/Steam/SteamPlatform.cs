#if STEAMWORKS_NET
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace oojjrs.oplat.steam
{
    internal class SteamPlatform : MonoBehaviour, MyPlatform.PlatformInterface
    {
        private sealed class SteamStatsService : MyStatsServiceInterface
        {
            private enum StatsTypeEnum
            {
                AverageRate,
                Float,
                Int,
            }

            private sealed class StatDefinition
            {
                public double AverageRateWindowSeconds { get; }
                public float DefaultFloatValue { get; }
                public int DefaultIntValue { get; }
                public StatsTypeEnum Type { get; }

                public StatDefinition(StatsTypeEnum type, int defaultIntValue, float defaultFloatValue, double averageRateWindowSeconds)
                {
                    Type = type;
                    DefaultIntValue = defaultIntValue;
                    DefaultFloatValue = defaultFloatValue;
                    AverageRateWindowSeconds = averageRateWindowSeconds;
                }
            }

            private readonly SemaphoreSlim _operationGate = new(1, 1);
            private readonly Dictionary<string, StatDefinition> _definitions = new(StringComparer.Ordinal);

            private Callback<UserStatsStored_t> _statsStoredCallback;
            private uint _appId;
            private bool _isInitialized;
            private int _mainThreadId;
            private TaskCompletionSource<bool> _storeSource;

            async Task MyStatsServiceInterface.AddAsync(string key, float value, CancellationToken cancellationToken)
            {
                EnsureReady(key, cancellationToken);
                EnsureFinite(value, nameof(value));
                await _operationGate.WaitAsync(cancellationToken);
                try
                {
                    EnsureReady(key, cancellationToken);
                    if (SteamUserStats.GetStat(key, out float currentValue) == false)
                        throw new InvalidOperationException($"Steam FLOAT stat '{key}' does not exist or is not published in App Admin.");

                    var addedValue = currentValue + value;
                    EnsureFinite(addedValue, nameof(value));
                    if (SteamUserStats.SetStat(key, addedValue) == false)
                        throw new InvalidOperationException($"Steam failed to add to FLOAT stat '{key}'.");

                    await StoreAsync(cancellationToken);
                }
                finally
                {
                    _operationGate.Release();
                }
            }

            async Task MyStatsServiceInterface.AddAsync(string key, int value, CancellationToken cancellationToken)
            {
                EnsureReady(key, cancellationToken);
                await _operationGate.WaitAsync(cancellationToken);
                try
                {
                    EnsureReady(key, cancellationToken);
                    if (SteamUserStats.GetStat(key, out int currentValue) == false)
                        throw new InvalidOperationException($"Steam INT stat '{key}' does not exist or is not published in App Admin.");

                    if (SteamUserStats.SetStat(key, checked(currentValue + value)) == false)
                        throw new InvalidOperationException($"Steam failed to add to INT stat '{key}'.");

                    await StoreAsync(cancellationToken);
                }
                finally
                {
                    _operationGate.Release();
                }
            }

            Task MyStatsServiceInterface.EnsureAsync(string definitionsJson, CancellationToken cancellationToken)
            {
                return MyPlatform.EnsureStatsAsync(this, definitionsJson, cancellationToken);
            }

            async Task MyStatsServiceInterface.EnsureAsync(string key, float defaultValue, CancellationToken cancellationToken)
            {
                EnsureFinite(defaultValue, nameof(defaultValue));
                await EnsureAsync(key, new StatDefinition(StatsTypeEnum.Float, 0, defaultValue, 0d), cancellationToken);
            }

            async Task MyStatsServiceInterface.EnsureAsync(string key, int defaultValue, CancellationToken cancellationToken)
            {
                await EnsureAsync(key, new StatDefinition(StatsTypeEnum.Int, defaultValue, 0f, 0d), cancellationToken);
            }

            async Task MyStatsServiceInterface.EnsureAverageRateAsync(string key, float defaultValue, double windowSeconds, CancellationToken cancellationToken)
            {
                EnsureFinite(defaultValue, nameof(defaultValue));
                EnsurePositiveFinite(windowSeconds, nameof(windowSeconds));
                await EnsureAsync(key, new StatDefinition(StatsTypeEnum.AverageRate, 0, defaultValue, windowSeconds), cancellationToken);
            }

            async Task MyStatsServiceInterface.ResetAsync(CancellationToken cancellationToken)
            {
                EnsureReady(cancellationToken);
                await _operationGate.WaitAsync(cancellationToken);
                try
                {
                    EnsureReady(cancellationToken);
                    await StoreAsync(() => SteamUserStats.ResetAllStats(false), "Steam failed to reset stats.", cancellationToken);
                }
                finally
                {
                    _operationGate.Release();
                }
            }

            async Task MyStatsServiceInterface.ResetAsync(string key, CancellationToken cancellationToken)
            {
                EnsureReady(key, cancellationToken);
                await _operationGate.WaitAsync(cancellationToken);
                try
                {
                    EnsureReady(key, cancellationToken);
                    var definition = GetDefinition(key);
                    bool isSet;
                    if (definition.Type == StatsTypeEnum.Int)
                        isSet = SteamUserStats.SetStat(key, definition.DefaultIntValue);
                    else if (definition.Type == StatsTypeEnum.Float)
                        isSet = SteamUserStats.SetStat(key, definition.DefaultFloatValue);
                    else
                        throw new NotSupportedException("Steam does not provide a single-stat reset API for AVGRATE stats.");

                    if (isSet == false)
                        throw new InvalidOperationException($"Steam failed to reset stat '{key}'.");

                    await StoreAsync(cancellationToken);
                }
                finally
                {
                    _operationGate.Release();
                }
            }

            async Task MyStatsServiceInterface.UpdateAverageRateAsync(string key, float count, double sessionLengthSeconds, CancellationToken cancellationToken)
            {
                EnsureReady(key, cancellationToken);
                EnsureFinite(count, nameof(count));
                EnsurePositiveFinite(sessionLengthSeconds, nameof(sessionLengthSeconds));
                await _operationGate.WaitAsync(cancellationToken);
                try
                {
                    EnsureReady(key, cancellationToken);
                    if (SteamUserStats.UpdateAvgRateStat(key, count, sessionLengthSeconds) == false)
                        throw new InvalidOperationException($"Steam AVGRATE stat '{key}' does not exist, is not published, or has a different type in App Admin.");

                    await StoreAsync(cancellationToken);
                }
                finally
                {
                    _operationGate.Release();
                }
            }

            private async Task EnsureAsync(string key, StatDefinition definition, CancellationToken cancellationToken)
            {
                EnsureReady(key, cancellationToken);
                await _operationGate.WaitAsync(cancellationToken);
                try
                {
                    EnsureReady(key, cancellationToken);
                    if (_definitions.TryGetValue(key, out var existingDefinition))
                    {
                        EnsureDefinition(key, existingDefinition, definition);
                        return;
                    }

                    var isFound = definition.Type == StatsTypeEnum.Int ? SteamUserStats.GetStat(key, out int _) : SteamUserStats.GetStat(key, out float _);
                    if (isFound == false)
                        throw new InvalidOperationException($"Steam {definition.Type} stat '{key}' does not exist or is not published in App Admin.");

                    _definitions.Add(key, definition);
                }
                finally
                {
                    _operationGate.Release();
                }
            }

            private static void EnsureDefinition(string key, StatDefinition existingDefinition, StatDefinition definition)
            {
                if (existingDefinition.Type != definition.Type)
                    throw new InvalidOperationException($"Steam stat '{key}' is already ensured as {existingDefinition.Type}.");

                if ((existingDefinition.DefaultIntValue != definition.DefaultIntValue) || (existingDefinition.DefaultFloatValue != definition.DefaultFloatValue) || (existingDefinition.AverageRateWindowSeconds != definition.AverageRateWindowSeconds))
                    throw new InvalidOperationException($"Steam stat '{key}' is already ensured with a different definition.");
            }

            private static void EnsureFinite(double value, string parameterName)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(parameterName);
            }

            private static void EnsureFinite(float value, string parameterName)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(parameterName);
            }

            private static void EnsurePositiveFinite(double value, string parameterName)
            {
                EnsureFinite(value, parameterName);
                if (value <= 0d)
                    throw new ArgumentOutOfRangeException(parameterName);
            }

            private StatDefinition GetDefinition(string key)
            {
                if (_definitions.TryGetValue(key, out var definition))
                    return definition;

                throw new InvalidOperationException($"Steam stat '{key}' is not ensured. Call EnsureAsync first.");
            }

            private void EnsureReady(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_isInitialized == false)
                    throw new InvalidOperationException("Steam stats are not initialized.");

                if (Environment.CurrentManagedThreadId != _mainThreadId)
                    throw new InvalidOperationException("Steam stats must be used on the Unity main thread.");
            }

            private void EnsureReady(string key, CancellationToken cancellationToken)
            {
                EnsureReady(cancellationToken);
                MyPlatform.EnsureStatsKey(key);
            }

            private async Task StoreAsync(CancellationToken cancellationToken)
            {
                await StoreAsync(SteamUserStats.StoreStats, "Steam failed to begin storing stats.", cancellationToken);
            }

            private async Task StoreAsync(Func<bool> beginStore, string failureMessage, CancellationToken cancellationToken)
            {
                EnsureReady(cancellationToken);
                if (_storeSource != null)
                    throw new InvalidOperationException("A Steam stats store is already in progress.");

                var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _storeSource = source;
                try
                {
                    if (beginStore() == false)
                        throw new InvalidOperationException(failureMessage);

                    await source.Task;
                }
                finally
                {
                    if (ReferenceEquals(_storeSource, source))
                        _storeSource = null;
                }
            }

            public void Initialize(uint appId)
            {
                if (_isInitialized)
                    return;

                _appId = appId;
                _mainThreadId = Environment.CurrentManagedThreadId;
                _statsStoredCallback = Callback<UserStatsStored_t>.Create(OnStatsStored);
                _isInitialized = true;
            }

            private void OnStatsStored(UserStatsStored_t result)
            {
                if (result.m_nGameID != _appId)
                    return;

                var source = _storeSource;
                if (source == null)
                    return;

                if (result.m_eResult == EResult.k_EResultOK)
                    source.TrySetResult(true);
                else
                    source.TrySetException(new InvalidOperationException($"Steam failed to store stats ({result.m_eResult})."));
            }

            public void Shutdown()
            {
                _isInitialized = false;
                _definitions.Clear();
                _storeSource?.TrySetCanceled();
                _storeSource = null;
                _statsStoredCallback?.Dispose();
                _statsStoredCallback = null;
            }
        }

        private const int ProfileSpriteLoadTimeoutMilliseconds = 5000;

        private readonly SteamNet _net = new();
        private readonly SteamStatsService _stats = new();
        private readonly SteamStorage _storage = new();

        private Callback<AvatarImageLoaded_t> _avatarImageLoadedCallback;
        private TaskCompletionSource<bool> _avatarImageLoadedSource;
        private bool _isInitialized;
        private bool _isRestartRequired;
        private Sprite _profileSprite;
        private Texture2D _profileSpriteTexture;
        private MyTimeServiceInterface _time = MyPlatform.CreateTimeServiceFromLocalClock();

        string MyPlatformServiceInterface.Account => SteamUser.GetSteamID().ToString();
        bool MyPlatformServiceInterface.IsAlive => (this != null) && _isInitialized;
        bool MyPlatformServiceInterface.IsRestartRequired => _isRestartRequired;
        MyNetInterface MyPlatformServiceInterface.Net => _net;
        string MyPlatformServiceInterface.Nickname => SteamFriends.GetPersonaName();
        Sprite MyPlatformServiceInterface.ProfileSprite => _profileSprite;
        MyStatsServiceInterface MyPlatformServiceInterface.Stats => _stats;
        MyStorageServiceInterface MyPlatformServiceInterface.Storage => _storage;
        MyTimeServiceInterface MyPlatformServiceInterface.Time => _time;

        private void OnDestroy()
        {
            var shutdownSteam = _isInitialized;
            _isInitialized = false;
            try
            {
                _storage.Shutdown();
            }
            finally
            {
                try
                {
                    _stats.Shutdown();
                }
                finally
                {
                    try
                    {
                        _net.Shutdown();
                    }
                    finally
                    {
                        try
                        {
                            _avatarImageLoadedSource?.TrySetCanceled();
                            _avatarImageLoadedCallback?.Dispose();

                            if (_profileSprite != null)
                                Destroy(_profileSprite);

                            if (_profileSpriteTexture != null)
                                Destroy(_profileSpriteTexture);
                        }
                        finally
                        {
                            if (shutdownSteam)
                                SteamAPI.Shutdown();
                        }
                    }
                }
            }
        }

        private void Update()
        {
            if (_isInitialized == false)
                return;

            SteamAPI.RunCallbacks();
            _net.Update();
        }

        async Task MyPlatform.PlatformInterface.RunAsync(MyPlatformInitializer.CallbackInterface callback, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_isInitialized)
                return;

            var appId = callback.AppId;
            if (appId == 0)
                throw new ArgumentOutOfRangeException(nameof(appId), "Steam App ID must be greater than zero.");

            if (Application.isEditor == false)
            {
                _isRestartRequired = SteamAPI.RestartAppIfNecessary(new AppId_t(appId));
                if (_isRestartRequired)
                    return;
            }

            var result = SteamAPI.InitEx(out var errorMessage);
            if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                throw new InvalidOperationException($"Steam initialization failed ({result}): {errorMessage}");

            _isInitialized = true;
            var actualAppId = SteamUtils.GetAppID().m_AppId;
            if (actualAppId != callback.AppId)
                throw new InvalidOperationException($"Steam initialized with App ID {actualAppId}, but {callback.AppId} was expected.");

            _stats.Initialize(appId);
            _time = MyPlatform.CreateTimeService(MyTime.FromUnixTimeSeconds(SteamUtils.GetServerRealTime()), true);
            _net.Initialize(callback.ChatResult, callback.FriendResult, callback.HostResult, callback.MemberResult, callback.PlayerResult, callback.RoomSwitchHandler, callback.RoomResult, _time);
            _profileSprite = await LoadProfileSpriteAsync(cancellationToken);
            _storage.Initialize();
            _net.PrepareLaunchJoinRequest();
        }

        private Sprite CreateProfileSprite(int imageHandle)
        {
            if (imageHandle <= 0)
                return null;

            if (SteamUtils.GetImageSize(imageHandle, out var width, out var height) == false)
                return null;

            var textureWidth = checked((int)width);
            var textureHeight = checked((int)height);
            var data = new byte[checked(textureWidth * textureHeight * 4)];
            if (SteamUtils.GetImageRGBA(imageHandle, data, data.Length) == false)
                return null;

            FlipVertically(data, textureWidth, textureHeight);

            _profileSpriteTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            _profileSpriteTexture.LoadRawTextureData(data);
            _profileSpriteTexture.Apply(false, true);

            var rect = new Rect(0f, 0f, _profileSpriteTexture.width, _profileSpriteTexture.height);
            return Sprite.Create(_profileSpriteTexture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static void FlipVertically(byte[] data, int width, int height)
        {
            var rowSize = checked(width * 4);
            var row = new byte[rowSize];

            for (var top = 0; top < height / 2; ++top)
            {
                var bottom = height - top - 1;
                Buffer.BlockCopy(data, top * rowSize, row, 0, rowSize);
                Buffer.BlockCopy(data, bottom * rowSize, data, top * rowSize, rowSize);
                Buffer.BlockCopy(row, 0, data, bottom * rowSize, rowSize);
            }
        }

        private async Task<Sprite> LoadProfileSpriteAsync(CancellationToken cancellationToken)
        {
            var steamId = SteamUser.GetSteamID();
            var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _avatarImageLoadedSource = source;
            _avatarImageLoadedCallback = Callback<AvatarImageLoaded_t>.Create(callback =>
            {
                if (callback.m_steamID == steamId)
                    source.TrySetResult(true);
            });

            try
            {
                var imageHandle = SteamFriends.GetLargeFriendAvatar(steamId);
                if (imageHandle != -1)
                    return CreateProfileSprite(imageHandle);

                if (await Task.WhenAny(source.Task, Task.Delay(ProfileSpriteLoadTimeoutMilliseconds, cancellationToken)) != source.Task)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return null;
                }

                await source.Task;
                cancellationToken.ThrowIfCancellationRequested();
                return CreateProfileSprite(SteamFriends.GetLargeFriendAvatar(steamId));
            }
            finally
            {
                _avatarImageLoadedSource = null;
                _avatarImageLoadedCallback.Dispose();
                _avatarImageLoadedCallback = null;
            }
        }
    }
}
#endif
