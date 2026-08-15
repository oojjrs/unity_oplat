using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNetLobbyService : MyNetLobbyServiceInterface
    {
        private readonly AnonymousServer _server = new();

        void MyNetLobbyServiceInterface.Refresh()
        {
            throw new System.NotImplementedException();
        }

        Task MyNetLobbyServiceInterface.StartAsync(MyNetLobbyServiceInterface.ConfigInterface config, MyNetLobbyServiceInterface.ResultInterface result)
        {
            throw new System.NotImplementedException();
        }

        void MyNetLobbyServiceInterface.Stop()
        {
            throw new System.NotImplementedException();
        }
    }
}
