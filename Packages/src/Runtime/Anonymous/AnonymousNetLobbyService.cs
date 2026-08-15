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
            try
            {
                config.CancellationToken.ThrowIfCancellationRequested();
                await _server.StartAsync(config.CancellationToken);

                result.OnOk(await _server.GetRoomsAsync(config.CancellationToken));
            }
            catch (System.OperationCanceledException) when (config.CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (System.Exception exception)
            {
                result.OnException(new MyNetSessionException("Failed to get anonymous rooms.", exception));
            }
        }

        void MyNetLobbyServiceInterface.Stop()
        {
            throw new System.NotImplementedException();
        }
    }
}
