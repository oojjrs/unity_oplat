using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousPlatform : MonoBehaviour, MyPlatform.PlatformInterface
    {
        async Task MyPlatform.PlatformInterface.RunAsync(CancellationToken cancellationToken)
        {
            // 한 턴 쉬어서 다른 녀석들과 타이밍을 맞춘다.
            await Task.Yield();
        }
    }
}
