using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNetLobbyService : MyNetLobbyServiceInterface
    {
        private readonly AnonymousServer _server;

        internal AnonymousNetLobbyService(AnonymousServer server)
        {
            _server = server;
        }

        void MyNetLobbyServiceInterface.Refresh()
        {
            throw new System.NotImplementedException();
        }

        async Task MyNetLobbyServiceInterface.StartAsync(MyNetLobbyServiceInterface.ConfigInterface config, MyNetLobbyServiceInterface.ResultInterface result)
        {
            config.CancellationToken.ThrowIfCancellationRequested();
            await _server.StartAsync(config.CancellationToken);
        }

        void MyNetLobbyServiceInterface.Stop()
        {
            throw new System.NotImplementedException();
        }
    }
}
