using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerExitRoom
    {
        [Serializable]
        internal record RequestArgument
        {
            public string PlayerId;
            public string RoomId;
        }

        internal static async Task<AnonymousServerResponse> RunAsync(string content, AnonymousServerRoom.State roomState, AnonymousServerSession session, CancellationToken cancellationToken)
        {
            var requestArgument = await AnonymousServer.ReadJsonAsync<RequestArgument>(content, "Invalid anonymous room exit request.", cancellationToken);
            if ((requestArgument == null) || string.IsNullOrWhiteSpace(requestArgument.PlayerId) || string.IsNullOrWhiteSpace(requestArgument.RoomId))
                throw new FormatException("Invalid anonymous room exit request.");

            var roomIndex = roomState.Rooms.FindIndex(secret => secret.Room.Id == requestArgument.RoomId);
            if (roomIndex < 0)
                return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.NotFound);

            var room = roomState.Rooms[roomIndex].Room;
            if ((session.Account != requestArgument.PlayerId) && (session.Account != room.HostId))
                return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Forbidden);

            var players = room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>();
            if (room.HostId == requestArgument.PlayerId)
            {
                roomState.RoomCodes.Remove(room.Code);
                roomState.Rooms.RemoveAt(roomIndex);
            }
            else
            {
                room.Players = players.Where(player => player.Id != requestArgument.PlayerId).ToArray();
            }

            return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Success);
        }
    }
}
