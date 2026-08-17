using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerExitRoom
    {
        public record RequestArgument
        {
            public string PlayerId { get; set; }
            public string RoomId { get; set; }
        }

        internal static async Task<AnonymousServerResponse> RunAsync(byte[] content, AnonymousServerRoom.State roomState, IReadOnlyDictionary<string, AnonymousServerSession> sessions, AnonymousServerSession session)
        {
            var requestArgument = await AnonymousServer.DeserializeAsync<RequestArgument>(content);
            if ((requestArgument == null) || string.IsNullOrWhiteSpace(requestArgument.PlayerId) || string.IsNullOrWhiteSpace(requestArgument.RoomId))
                throw new FormatException("Invalid anonymous room exit request.");

            var roomIndex = roomState.Rooms.FindIndex(secret => secret.Room.Id == requestArgument.RoomId);
            if (roomIndex < 0)
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.NotFound);

            var room = roomState.Rooms[roomIndex].Room;
            if ((session.Account != requestArgument.PlayerId) && (session.Account != room.HostId))
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

            var players = room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>();
            if (room.HostId == requestArgument.PlayerId)
            {
                roomState.RoomCodes.Remove(room.Code);
                roomState.Rooms.RemoveAt(roomIndex);
            }
            else
            {
                room.Players = players.Where(player => player.Id != requestArgument.PlayerId).ToArray();
                if (room.Players.Length != players.Length)
                    await AnonymousServerRoom.NotifyUpdatedAsync(room, sessions, requestArgument.PlayerId);
            }

            return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Success);
        }
    }
}
