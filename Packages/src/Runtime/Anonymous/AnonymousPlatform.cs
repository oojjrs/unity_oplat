using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousPlatform : MonoBehaviour, MyPlatform.PlatformInterface
    {
        private readonly AnonymousNet _net = new();

        private bool _isInitialized;
        private Sprite _profileSprite;

        string MyPlatformServiceInterface.Account
        {
            get
            {
                var account = SystemInfo.deviceUniqueIdentifier;
                if (string.IsNullOrEmpty(account) == false && account != SystemInfo.unsupportedIdentifier)
                    return account;

                return ((MyPlatformServiceInterface)this).Nickname;
            }
        }
        bool MyPlatformServiceInterface.IsAlive => (this != null) && _isInitialized;
        bool MyPlatformServiceInterface.IsRestartRequired => false;
        MyNetInterface MyPlatformServiceInterface.Net => _net;
        string MyPlatformServiceInterface.Nickname
        {
            get
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
        Sprite MyPlatformServiceInterface.ProfileSprite => _profileSprite;

        private void OnDestroy()
        {
            _net.Shutdown();
        }

        async Task MyPlatform.PlatformInterface.RunAsync(MyPlatformInitializer.CallbackInterface callback, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_isInitialized)
                return;

            var profileSpriteRequest = Resources.LoadAsync<Sprite>("AnonymousProfile");
            await Awaitable.FromAsyncOperation(profileSpriteRequest, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            _profileSprite = profileSpriteRequest.asset as Sprite;
            _isInitialized = true;
        }
    }
}
