#if STEAMWORKS_NET
using System.Threading.Tasks;

namespace oojjrs.oplat.steam
{
    internal sealed class SteamNetChatService : MyNetChatServiceInterface
    {
        internal const int MessageByteCountMax = 4089;

        private readonly SteamNet Net;

        int MyNetChatServiceInterface.MessageByteCountMax => MessageByteCountMax;

        internal SteamNetChatService(SteamNet net)
        {
            Net = net;
        }

        Task MyNetChatServiceInterface.ExitAsync(MyNetChatServiceInterface.ExitConfigInterface config, MyNetChatServiceInterface.ExitResultInterface result)
        {
            return Net.ExitChatAsync(config, result);
        }

        Task MyNetChatServiceInterface.JoinAsync(MyNetChatServiceInterface.JoinConfigInterface config, MyNetChatServiceInterface.JoinResultInterface result)
        {
            return Net.JoinChatAsync(config, result);
        }

        Task MyNetChatServiceInterface.SendAsync(MyNetChatServiceInterface.SendConfigInterface config, MyNetChatServiceInterface.SendResultInterface result)
        {
            return Net.SendChatAsync(config, result);
        }
    }
}
#endif
