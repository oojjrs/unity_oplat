using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousPlatform : MonoBehaviour, MyPlatform.PlatformInterface
    {
        private bool _isInitialized;
        private Sprite _profileImage;

        string MyPlatformServiceInterface.Account
        {
            get
            {
                var account = SystemInfo.deviceUniqueIdentifier;
                if (string.IsNullOrEmpty(account) == false && account != SystemInfo.unsupportedIdentifier)
                    return account;

                return DeviceName;
            }
        }
        bool MyPlatformServiceInterface.IsAlive => (this != null) && _isInitialized;
        string MyPlatformServiceInterface.Nickname => DeviceName;
        Sprite MyPlatformServiceInterface.ProfileImage => _profileImage;

        private string DeviceName
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

        async Task MyPlatform.PlatformInterface.RunAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_isInitialized)
                return;

            var profileImageRequest = Resources.LoadAsync<Sprite>("AnonymousProfile");
            await Awaitable.FromAsyncOperation(profileImageRequest, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            _profileImage = profileImageRequest.asset as Sprite;
            _isInitialized = true;
        }
    }
}
