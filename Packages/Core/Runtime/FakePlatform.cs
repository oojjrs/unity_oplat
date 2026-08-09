using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat
{
    public sealed class FakePlatform : PlatformInterface
    {
        readonly PlatformKindEnum _kind;
        readonly PlatformUser _user;
        bool _isInitialized;

        bool PlatformInterface.IsInitialized => _isInitialized;
        PlatformKindEnum PlatformInterface.Kind => _kind;
        PlatformUser PlatformInterface.User => _user;

        public FakePlatform(PlatformKindEnum kind, PlatformUser user)
        {
            _kind = kind;
            _user = user;
        }

        Task PlatformInterface.InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _isInitialized = true;
            return Task.CompletedTask;
        }

        void PlatformInterface.RunCallbacks()
        {
        }

        void PlatformInterface.Shutdown()
        {
            _isInitialized = false;
        }
    }
}
