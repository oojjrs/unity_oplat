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

#if UNITY_EDITOR
        private static MyPlatformInitializer.CallbackInterface GetEditorCallback()
        {
            var selectedGameObject = UnityEditor.Selection.activeGameObject;
            if (selectedGameObject != null)
            {
                var selectedInitializer = selectedGameObject.GetComponentInParent<MyPlatformInitializer>(true);
                if (selectedInitializer != null)
                {
                    var selectedCallback = selectedInitializer.GetComponent<MyPlatformInitializer.CallbackInterface>();
                    if ((selectedCallback != null) && (selectedCallback.InitialType == MyPlatformTypeEnum.Anonymous))
                        return selectedCallback;
                }
            }

            var result = default(MyPlatformInitializer.CallbackInterface);
            foreach (var initializer in UnityEngine.Object.FindObjectsByType<MyPlatformInitializer>(FindObjectsInactive.Include))
            {
                var callback = initializer.GetComponent<MyPlatformInitializer.CallbackInterface>();
                if ((callback == null) || (callback.InitialType != MyPlatformTypeEnum.Anonymous))
                    continue;

                if (result != null)
                    return null;

                result = callback;
            }

            return result;
        }
#endif

        private static void GetIdentity(string instanceId, out string account, out string nickname)
        {
            instanceId = instanceId?.Trim();
            if (string.IsNullOrEmpty(instanceId) == false)
            {
                account = instanceId;
                nickname = instanceId;
                return;
            }

            nickname = GetNickname();
            account = GetAccount(nickname);
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

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Oplat/Open Anonymous Friend List")]
        private static void OpenFriendList()
        {
            var platform = UnityEngine.Object.FindAnyObjectByType<AnonymousPlatform>();
            uint appId;
            string account;
            if ((platform != null) && platform._isInitialized)
            {
                appId = platform._appId;
                account = platform._account;
            }
            else
            {
                var callback = GetEditorCallback();
                if (callback == null)
                {
                    UnityEditor.EditorUtility.DisplayDialog("Anonymous Friend List", "Select a GameObject with one Anonymous MyPlatformInitializer, or keep exactly one in the open scenes.", "OK");
                    return;
                }

                appId = callback.AppId;
                GetIdentity(callback.AnonymousInstanceId, out account, out _);
            }

            var path = AnonymousServer.GetFriendStoragePath(appId, account);
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

            var appId = callback.AppId;
            GetIdentity(callback.AnonymousInstanceId, out var account, out var nickname);

            _account = account;
            _appId = appId;
            _nickname = nickname;

            var profileSpriteRequest = Resources.LoadAsync<Sprite>("AnonymousProfile");
            await Awaitable.FromAsyncOperation(profileSpriteRequest, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            _profileSprite = profileSpriteRequest.asset as Sprite;

            await Net.AuthenticateAsync(_account, _nickname, appId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            Net.Initialize(_account, callback.ChatResult, callback.FriendResult, callback.HostResult, callback.MemberResult, callback.PlayerResult, callback.RoomResult);
            _storage.Initialize(appId, _account);

            _isInitialized = true;
        }
    }
}
