using System.Collections.Generic;
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

        private bool IsHost => throw new System.NotImplementedException();
        private IEnumerable<MyNetPlayerInterface> PlayersWithoutHost => throw new System.NotImplementedException();

        private void OnDestroy()
        {
            Net.Shutdown();
        }

        private void Update()
        {
            if (Net.HostService.HasResponses())
            {
                // 서버 -> 클라 응답 전송
                while (Net.HostService.TryDequeue(out var response))
                {
                    // 나에게는 즉시 수행
                    Net.MemberService.Receive(response);

                    var bytes = MyNetSerializer.Serialize(response);
                    foreach (var player in PlayersWithoutHost)
                    {
                        // 어케 보내지?
                    }
                }

                Net.MemberService.HandleResponses();
            }

            // 의도적으로 요청은 응답보다 늦게 처리하는 것이다.
            if (Net.MemberService.HasRequest())
            {
                // 클라 -> 서버 요청 적재
                while (Net.MemberService.TryDequeue(out var request))
                {
                    if (IsHost)
                    {
                        // 나에게는 즉시 수행
                        Net.HostService.Receive(request);
                    }
                    else
                    {
                        // TODO: 어케 보내지?
                        var bytes = MyNetSerializer.Serialize(request);
                    }
                }

                Net.HostService.HandleRequests();
            }
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

            Net.HostResult = callback.HostResult;
            Net.MemberResult = callback.MemberResult;

            _isInitialized = true;
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
    }
}
