using System.Threading.Tasks;

namespace oojjrs.oplat.steam
{
#if STEAMWORKS_NET
    internal class SteamNetPlayerService : MyNetPlayerServiceInterface
    {
        private readonly SteamNet Net;

        internal SteamNetPlayerService(SteamNet net)
        {
            Net = net;
        }

        Task MyNetPlayerServiceInterface.UpdateAsync(MyNetPlayerServiceInterface.UpdateConfigInterface config, MyNetPlayerServiceInterface.UpdateResultInterface result)
        {
            return Net.UpdatePlayerAsync(config, result);
        }
    }
#endif
}
