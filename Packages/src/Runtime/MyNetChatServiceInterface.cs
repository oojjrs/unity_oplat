using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat
{
    public interface MyNetChatServiceInterface
    {
        public interface ExitConfigInterface
        {
            CancellationToken CancellationToken { get; }
            string RoomId { get; }
        }

        public interface ExitResultInterface : MyNetInterface.CatchInterface
        {
            void OnOk(string roomId);
        }

        public interface JoinConfigInterface
        {
            CancellationToken CancellationToken { get; }
            string RoomId { get; }
        }

        public interface JoinResultInterface : MyNetInterface.CatchInterface
        {
            void OnOk(string roomId);
        }

        public interface SendConfigInterface
        {
            CancellationToken CancellationToken { get; }
            string Message { get; }
            string RoomId { get; }
        }

        public interface SendResultInterface : MyNetInterface.CatchInterface
        {
            void OnOk(string roomId);
        }

        int MessageByteCountMax { get; }

        Task ExitAsync(ExitConfigInterface config, ExitResultInterface result);
        Task JoinAsync(JoinConfigInterface config, JoinResultInterface result);
        Task SendAsync(SendConfigInterface config, SendResultInterface result);
    }
}
