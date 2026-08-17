using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousPlatform : MonoBehaviour, MyPlatform.PlatformInterface
    {
        private readonly AnonymousNet Net = new();

        private string _account;
        private bool _isInitialized;
        private string _nickname;
        private Sprite _profileSprite;

        string MyPlatformServiceInterface.Account => _account ?? GetAccount(GetNickname());
        bool MyPlatformServiceInterface.IsAlive => (this != null) && _isInitialized;
        bool MyPlatformServiceInterface.IsRestartRequired => false;
        MyNetInterface MyPlatformServiceInterface.Net => Net;
        string MyPlatformServiceInterface.Nickname => _nickname ?? GetNickname();
        Sprite MyPlatformServiceInterface.ProfileSprite => _profileSprite;

        private void OnDestroy()
        {
            Net.Shutdown();
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

        async Task MyPlatform.PlatformInterface.RunAsync(MyPlatformInitializer.CallbackInterface callback, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_isInitialized)
                return;

            var nickname = GetNickname();
            var account = GetAccount(nickname);
            var instanceId = callback.AnonymousInstanceId?.Trim();
            if (string.IsNullOrEmpty(instanceId) == false)
            {
                account = $"{account}:{callback.AppId}:{instanceId}";
                nickname = $"{nickname} [{instanceId}]";
            }

            _account = account;
            _nickname = nickname;

            var profileSpriteRequest = Resources.LoadAsync<Sprite>("AnonymousProfile");
            await Awaitable.FromAsyncOperation(profileSpriteRequest, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            _profileSprite = profileSpriteRequest.asset as Sprite;

            await Net.AuthenticateAsync(_account, _nickname, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            Net.Initialize(_account, callback.HostResult, callback.MemberResult, callback.RoomResult);

            _isInitialized = true;
        }
    }
}
