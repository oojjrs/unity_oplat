using System.Threading.Tasks;

namespace oojjrs.oplat.steam
{
#if STEAMWORKS_NET
    internal class SteamNetLobbyService : MyNetLobbyServiceInterface
    {
        private readonly SteamNet Net;

        internal SteamNetLobbyService(SteamNet net)
        {
            Net = net;
        }

        Task MyNetLobbyServiceInterface.RefreshAsync(MyNetLobbyServiceInterface.ResultInterface result)
        {
            return Net.RefreshLobbyAsync(result, System.Threading.CancellationToken.None);
        }

        Task MyNetLobbyServiceInterface.StartAsync(MyNetLobbyServiceInterface.ConfigInterface config, MyNetLobbyServiceInterface.ResultInterface result)
        {
            return Net.StartLobbyAsync(config, result);
        }

        void MyNetLobbyServiceInterface.Stop()
        {
            Net.StopLobby();
        }
    }
#endif
}
