using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat
{
    public interface MyNetRoomServiceInterface
    {
        public interface CreateConfigInterface
        {
            string Account { get; }
            CancellationToken CancellationToken { get; }
            bool IsLocked { get; }
            bool IsPrivate { get; }
            int MaxPlayers { get; }
            string Password { get; }
            IEnumerable<MyNetInterface.Field> PlayerFields { get; }
            string PlayerNickname { get; }
            IEnumerable<MyNetInterface.Field> RoomFields { get; }
            string Title { get; }
        }

        public interface CreateResultInterface : MyNetInterface.CatchInterface
        {
            void OnOk(MyNetRoomInterface room);
        }

        public interface ExitConfigInterface
        {
            CancellationToken CancellationToken { get; }
            string PlayerId { get; }
            string RoomId { get; }
        }

        public interface ExitResultInterface : MyNetInterface.CatchInterface
        {
            void OnOk(string roomId, string playerId);
        }

        public interface JoinConfigInterface
        {
            string Account { get; }
            CancellationToken CancellationToken { get; }
            string Code { get; }
            string Password { get; }
            IEnumerable<MyNetInterface.Field> PlayerFields { get; }
            string PlayerNickname { get; }
            string RoomId { get; }
        }

        public interface JoinResultInterface : MyNetInterface.CatchInterface
        {
            void OnOk(MyNetRoomInterface room);
        }

        public interface UpdateConfigInterface
        {
            CancellationToken CancellationToken { get; }
            bool IsPrivate { get; }
            IEnumerable<MyNetInterface.Field> RoomFields { get; }
            string RoomId { get; }
        }

        public interface UpdateResultInterface : MyNetInterface.CatchInterface
        {
            void OnOk(MyNetRoomInterface room);
        }

        Task CreateAsync(CreateConfigInterface config, CreateResultInterface result);
        Task ExitAsync(ExitConfigInterface config, ExitResultInterface result);
        Task JoinAsync(JoinConfigInterface config, JoinResultInterface result);
        Task UpdateAsync(UpdateConfigInterface config, UpdateResultInterface result);
    }
}
