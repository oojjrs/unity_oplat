using System.Threading.Tasks;

namespace oojjrs.oplat.steam
{
    internal class SteamNetLobbyService : MyNetLobbyServiceInterface
    {
        Task MyNetLobbyServiceInterface.RefreshAsync(MyNetLobbyServiceInterface.ResultInterface result)
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
