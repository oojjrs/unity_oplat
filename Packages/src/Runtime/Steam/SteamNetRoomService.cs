using System.Threading.Tasks;

using System.Threading;

namespace oojjrs.oplat.steam
{
#if STEAMWORKS_NET
    internal class SteamNetRoomService : MyNetRoomServiceInterface
    {
        private readonly SteamNet Net;
        private readonly MyNetRoomSwitcher Switcher;

        internal SteamNetRoomService(SteamNet net)
        {
            Net = net;
            Switcher = new(() => Net.Account, Net.GetCurrentRoomAsync, Net.ExitRoomAsync, Net.JoinRoomAsync);
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

        Task MyNetRoomServiceInterface.SwitchAsync(MyNetRoomServiceInterface.JoinConfigInterface config, MyNetRoomServiceInterface.JoinResultInterface result)
        {
            return Switcher.SwitchAsync(config, result);
        }

        Task MyNetRoomServiceInterface.UpdateAsync(MyNetRoomServiceInterface.UpdateConfigInterface config, MyNetRoomServiceInterface.UpdateResultInterface result)
        {
            return Net.UpdateRoomAsync(config, result);
        }

        internal Task HandleSwitchRequestAsync(string playerId, string roomId, MyNetRoomSwitchHandlerInterface handler, CancellationToken cancellationToken)
        {
            return Switcher.HandleRequestAsync(playerId, roomId, handler, cancellationToken);
        }
    }
#endif
}
