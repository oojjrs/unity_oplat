#if STEAMWORKS_NET
using System;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace oojjrs.oplat.steam
{
    internal class SteamPlatform : MonoBehaviour, MyPlatform.PlatformInterface
    {
        private const int ProfileSpriteLoadTimeoutMilliseconds = 5000;

        private readonly SteamNet _net = new();
        private readonly SteamStorage _storage = new();

        private Callback<AvatarImageLoaded_t> _avatarImageLoadedCallback;
        private TaskCompletionSource<bool> _avatarImageLoadedSource;
        private bool _isInitialized;
        private bool _isRestartRequired;
        private Sprite _profileSprite;
        private Texture2D _profileSpriteTexture;

        string MyPlatformServiceInterface.Account => SteamUser.GetSteamID().ToString();
        bool MyPlatformServiceInterface.IsAlive => (this != null) && _isInitialized;
        bool MyPlatformServiceInterface.IsRestartRequired => _isRestartRequired;
        MyNetInterface MyPlatformServiceInterface.Net => _net;
        string MyPlatformServiceInterface.Nickname => SteamFriends.GetPersonaName();
        Sprite MyPlatformServiceInterface.ProfileSprite => _profileSprite;
        MyStorageServiceInterface MyPlatformServiceInterface.Storage => _storage;

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

            _storage.Initialize();
            _net.Initialize(callback.ChatResult, callback.HostResult, callback.MemberResult, callback.PlayerResult, callback.RoomResult);
            _profileSprite = await LoadProfileSpriteAsync(cancellationToken);
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
