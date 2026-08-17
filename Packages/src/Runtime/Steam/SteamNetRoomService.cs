using System.Threading.Tasks;

namespace oojjrs.oplat.steam
{
#if STEAMWORKS_NET
    internal class SteamNetRoomService : MyNetRoomServiceInterface
    {
        private readonly SteamNet Net;

        internal SteamNetRoomService(SteamNet net)
        {
            Net = net;
        }

        Task MyNetRoomServiceInterface.CreateAsync(MyNetRoomServiceInterface.CreateConfigInterface config, MyNetRoomServiceInterface.CreateResultInterface result)
        {
            return Net.CreateRoomAsync(config, result);
        }

        Task MyNetRoomServiceInterface.ExitAsync(MyNetRoomServiceInterface.ExitConfigInterface config, MyNetRoomServiceInterface.ExitResultInterface result)
        {
            return Net.ExitRoomAsync(config, result);
        }

        Task MyNetRoomServiceInterface.JoinAsync(MyNetRoomServiceInterface.JoinConfigInterface config, MyNetRoomServiceInterface.JoinResultInterface result)
        {
            return Net.JoinRoomAsync(config, result);
        }

        Task MyNetRoomServiceInterface.UpdateAsync(MyNetRoomServiceInterface.UpdateConfigInterface config, MyNetRoomServiceInterface.UpdateResultInterface result)
        {
            return Net.UpdateRoomAsync(config, result);
        }
    }
#endif
}
