using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat
{
    public interface MyNetPlayerServiceInterface
    {
        public interface UpdateConfigInterface
        {
            CancellationToken CancellationToken { get; }
            IEnumerable<MyNetInterface.Field> PlayerFields { get; }
            string PlayerId { get; }
            string RoomId { get; }
        }

        public interface UpdateResultInterface : MyNetInterface.CatchInterface
        {
            void OnOk(MyNetRoomInterface room);
        }

        Task UpdateAsync(UpdateConfigInterface config, UpdateResultInterface result);
    }
}
