using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousPlatform : MonoBehaviour, MyPlatform.PlatformInterface
    {
        private readonly AnonymousNet Net = new();
        private readonly AnonymousStorage _storage = new();

        private string _account;
        private uint _appId;
        private bool _isInitialized;
        private string _nickname;
        private Sprite _profileSprite;

        string MyPlatformServiceInterface.Account => _account ?? GetAccount(GetNickname());
        bool MyPlatformServiceInterface.IsAlive => (this != null) && _isInitialized;
        bool MyPlatformServiceInterface.IsRestartRequired => false;
        MyNetInterface MyPlatformServiceInterface.Net => Net;
        string MyPlatformServiceInterface.Nickname => _nickname ?? GetNickname();
        Sprite MyPlatformServiceInterface.ProfileSprite => _profileSprite;
        MyStorageServiceInterface MyPlatformServiceInterface.Storage => _storage;

        private void OnDestroy()
        {
            try
            {
                _storage.Shutdown();
            }
            finally
            {
                Net.Shutdown();
            }
        }

        private async void Start()
        {
            var cancellationToken = destroyCancellationToken;
            try
            {
                await Net.RunServiceLoopAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        private void Update()
        {
            Net.LobbyService.Update();
        }

        private static string GetAccount(string nickname)
        {
            var account = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(account) == false && account != SystemInfo.unsupportedIdentifier)
                return account;

            return nickname;
        }

        private static string GetNickname()
        {
            var deviceName = SystemInfo.deviceName;
            if (string.IsNullOrEmpty(deviceName) == false && deviceName != SystemInfo.unsupportedIdentifier)
                return deviceName;

            var productName = Application.productName;
            if (string.IsNullOrEmpty(productName) == false)
                return productName;

            return nameof(AnonymousPlatform);
        }

        private static string GetStorageProjectKey()
        {
            var identifier = Application.identifier?.Trim();
            if (string.IsNullOrEmpty(identifier) == false)
                return identifier;

            var companyName = Application.companyName?.Trim();
            var productName = Application.productName?.Trim();
            if (string.IsNullOrEmpty(companyName) == false && string.IsNullOrEmpty(productName) == false)
                return $"{companyName}.{productName}";

            if (string.IsNullOrEmpty(productName) == false)
                return productName;

            if (string.IsNullOrEmpty(companyName) == false)
                return companyName;

            return nameof(AnonymousPlatform);
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Oplat/Open Anonymous Friend List")]
        private static void OpenFriendList()
        {
            var platform = UnityEngine.Object.FindFirstObjectByType<AnonymousPlatform>();
            if ((platform == null) || (platform._isInitialized == false))
            {
                UnityEditor.EditorUtility.DisplayDialog("Anonymous Friend List", "Start Play Mode with the Anonymous platform before opening its friend list.", "OK");
                return;
            }

            var path = AnonymousServer.GetFriendStoragePath(platform._appId, GetStorageProjectKey(), platform._account);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path) == false)
                File.WriteAllText(path, "[]", new UTF8Encoding(false));

            UnityEditor.EditorUtility.OpenWithDefaultApp(path);
        }
#endif

        async Task MyPlatform.PlatformInterface.RunAsync(MyPlatformInitializer.CallbackInterface callback, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_isInitialized)
                return;

            var nickname = GetNickname();
            var account = GetAccount(nickname);
            var appId = callback.AppId;
            var instanceId = callback.AnonymousInstanceId?.Trim();
            if (string.IsNullOrEmpty(instanceId) == false)
            {
                account = $"{account}:{appId}:{instanceId}";
                nickname = $"{nickname} [{instanceId}]";
            }

            _account = account;
            _appId = appId;
            _nickname = nickname;

            var profileSpriteRequest = Resources.LoadAsync<Sprite>("AnonymousProfile");
            await Awaitable.FromAsyncOperation(profileSpriteRequest, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            _profileSprite = profileSpriteRequest.asset as Sprite;

            var projectKey = GetStorageProjectKey();
            await Net.AuthenticateAsync(_account, _nickname, appId, projectKey, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            Net.Initialize(_account, callback.ChatResult, callback.FriendResult, callback.HostResult, callback.MemberResult, callback.PlayerResult, callback.RoomResult);
            _storage.Initialize(appId, projectKey, _account);

            _isInitialized = true;
        }
    }
}
