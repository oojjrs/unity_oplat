using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat
{
    public interface PlatformInterface
    {
        bool IsInitialized { get; }
        PlatformKindEnum Kind { get; }
        PlatformUser User { get; }

        Task InitializeAsync(CancellationToken cancellationToken);
        void RunCallbacks();
        void Shutdown();
    }
}
