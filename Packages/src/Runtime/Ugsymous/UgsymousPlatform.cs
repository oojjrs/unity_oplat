using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Options;
using Unity.Services.Vivox;
using UnityEngine;

namespace oojjrs.oplat.ugsymous
{
    internal sealed class UgsymousPlatform : MonoBehaviour, MyPlatform.PlatformInterface
    {
        private const string AchievementStorageKey = "oplat_achievements_v1";
        private const string StatsStorageKey = "oplat_stats_v1";

        private readonly UgsymousStorage _storage = new();
        private readonly MyTimeServiceInterface _time = MyPlatform.CreateTimeServiceFromLocalClock();
        private MyAchievementServiceInterface _achievements;
        private UgsymousNet _net;
        private string _nickname;
        private Sprite _profileSprite;
        private MyStatsServiceInterface _stats;

        string MyPlatformServiceInterface.Account => AuthenticationService.Instance.PlayerId;
        MyAchievementServiceInterface MyPlatformServiceInterface.Achievements => _achievements;
        bool MyPlatformServiceInterface.IsAlive => (this != null) && AuthenticationService.Instance.IsAuthorized;
        bool MyPlatformServiceInterface.IsRestartRequired => false;
        MyNetInterface MyPlatformServiceInterface.Net => _net;
        string MyPlatformServiceInterface.Nickname => _nickname;
        Sprite MyPlatformServiceInterface.ProfileSprite => _profileSprite;
        MyStatsServiceInterface MyPlatformServiceInterface.Stats => _stats;
        MyStorageServiceInterface MyPlatformServiceInterface.Storage => _storage;
        MyTimeServiceInterface MyPlatformServiceInterface.Time => _time;

        async Task MyPlatformServiceInterface.ResetAllProgressAsync(CancellationToken cancellationToken)
        {
            await _stats.ResetAsync(cancellationToken);
            await _achievements.ResetAsync(cancellationToken);
        }

        internal static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
                builder.Append(value.ToString("x2"));

            return builder.ToString();
        }

        private static string ToProfile(string value)
        {
            using (var sha256 = SHA256.Create())
                return $"oplat-{ToHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(value)))[..24]}";
        }

        private void OnDestroy() => _net?.Dispose();

        private void Update() => _net?.Update();

        async Task MyPlatform.PlatformInterface.RunAsync(MyPlatformInitializer.CallbackInterface callback, CancellationToken cancellationToken)
        {
            var profile = string.IsNullOrWhiteSpace(callback.AnonymousInstanceId) ? "default" : ToProfile(callback.AnonymousInstanceId);
            await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(profile));
            cancellationToken.ThrowIfCancellationRequested();

            await AuthenticateAsync(profile);
            cancellationToken.ThrowIfCancellationRequested();

            var authentication = AuthenticationService.Instance;
            _nickname = await authentication.GetPlayerNameAsync();
            _profileSprite = Resources.Load<Sprite>("AnonymousProfile");
            _achievements = MyPlatform.CreateAchievementService(ReadAchievementsAsync, WriteAchievementsAsync);
            _stats = MyPlatform.CreateStatsService(ReadStatsAsync, WriteStatsAsync);
            await FriendsService.Instance.InitializeAsync(new InitializeOptions().WithMemberPresence(true).WithMemberProfile(true));
            await FriendsService.Instance.SetPresenceAsync(Availability.Online, new UgsymousFriendActivity());
            await VivoxService.Instance.InitializeAsync();
            if (VivoxService.Instance.IsLoggedIn == false)
                await VivoxService.Instance.LoginAsync();

            cancellationToken.ThrowIfCancellationRequested();
            _net = new UgsymousNet(authentication.PlayerId, callback, _time);
        }

        private static async Task<(bool IsFound, byte[] Data)> ReadStatsAsync(CancellationToken cancellationToken)
        {
            return await ReadProgressAsync(StatsStorageKey, cancellationToken);
        }

        private static async Task<(bool IsFound, byte[] Data)> ReadAchievementsAsync(CancellationToken cancellationToken)
        {
            return await ReadProgressAsync(AchievementStorageKey, cancellationToken);
        }

        private static async Task<(bool IsFound, byte[] Data)> ReadProgressAsync(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var items = await CloudSaveService.Instance.Files.Player.ListAllAsync();
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var item in items)
            {
                if (item.Key != key)
                    continue;

                var data = await CloudSaveService.Instance.Files.Player.LoadBytesAsync(key);
                cancellationToken.ThrowIfCancellationRequested();
                return (true, data);
            }

            return (false, System.Array.Empty<byte>());
        }

        private static async Task WriteStatsAsync(byte[] data, CancellationToken cancellationToken)
        {
            await WriteProgressAsync(StatsStorageKey, data, cancellationToken);
        }

        private static async Task WriteAchievementsAsync(byte[] data, CancellationToken cancellationToken)
        {
            await WriteProgressAsync(AchievementStorageKey, data, cancellationToken);
        }

        private static async Task WriteProgressAsync(string key, byte[] data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await CloudSaveService.Instance.Files.Player.SaveAsync(key, data);
        }

        private async Task AuthenticateAsync(string profile)
        {
            var authentication = AuthenticationService.Instance;
            if (authentication.Profile != profile)
            {
                if (VivoxService.Instance.IsLoggedIn)
                    await VivoxService.Instance.LogoutAsync();

                if (authentication.IsSignedIn)
                    authentication.SignOut();

                authentication.SwitchProfile(profile);
            }

            if (authentication.IsAuthorized == false)
                await authentication.SignInAnonymouslyAsync();
        }
    }
}
