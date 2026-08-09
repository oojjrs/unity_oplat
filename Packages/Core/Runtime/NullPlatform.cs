using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat
{
    public sealed class NullPlatform : PlatformInterface
    {
        readonly PlatformUser _user;
        bool _isInitialized;

        bool PlatformInterface.IsInitialized => _isInitialized;
        PlatformKindEnum PlatformInterface.Kind => PlatformKindEnum.Null;
        PlatformUser PlatformInterface.User => _user;

        public NullPlatform(string id = "local", string displayName = "Local Player")
        {
            _user = new(id, displayName);
        }

        async Task PlatformInterface.InitializeAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            cancellationToken.ThrowIfCancellationRequested();
            _isInitialized = true;
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
