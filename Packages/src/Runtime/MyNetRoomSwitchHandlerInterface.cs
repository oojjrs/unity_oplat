using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat
{
    public interface MyNetRoomSwitchHandlerInterface : MyNetRoomServiceInterface.JoinResultInterface
    {
        public interface PreparationInterface
        {
            string Password { get; }
            IEnumerable<MyNetInterface.Field> PlayerFields { get; }
            string PlayerNickname { get; }
        }

        Task<PreparationInterface> PrepareAsync(string playerId, string roomId, CancellationToken cancellationToken);
    }
}
