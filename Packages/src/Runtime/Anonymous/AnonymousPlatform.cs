using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousPlatform : MonoBehaviour, MyPlatform.PlatformInterface
    {
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
        string MyPlatformServiceInterface.Nickname => DeviceName;

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
            // 한 턴 쉬어서 다른 녀석들과 타이밍을 맞춘다.
            await Task.Yield();
        }
    }
}
