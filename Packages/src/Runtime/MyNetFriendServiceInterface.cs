using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat
{
    public interface MyNetFriendServiceInterface
    {
        public interface ConfigInterface
        {
            CancellationToken CancellationToken { get; }
            int PollingDelaySeconds { get; }
        }

        public interface InviteConfigInterface
        {
            CancellationToken CancellationToken { get; }
            string PlayerId { get; }
            string RoomId { get; }
        }

        public interface InviteResultInterface : MyNetInterface.CatchInterface
        {
            void OnOk(string roomId, string playerId);
        }

        public interface RequestAddConfigInterface
        {
            CancellationToken CancellationToken { get; }
            string PlayerId { get; }
        }

        public interface RequestAddResultInterface : MyNetInterface.CatchInterface
        {
            void OnOk(string playerId);
        }

        public interface ResultInterface : MyNetInterface.CatchInterface
        {
            void OnOk(IEnumerable<MyNetFriendInterface> friends);
        }

        Task InviteAsync(InviteConfigInterface config, InviteResultInterface result);
        Task RefreshAsync(ResultInterface result);
        Task RequestAddAsync(RequestAddConfigInterface config, RequestAddResultInterface result);
        Task StartAsync(ConfigInterface config, ResultInterface result);
        void Stop();
    }
}
