using System;
using System.Linq;
using System.Net;
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

        internal static async Task RunAsync(HttpListenerRequest request, HttpListenerResponse response, AnonymousServerRoom.State roomState, AnonymousServerSession.State sessionState)
        {
            if (request.HttpMethod != "POST")
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            if (sessionState.TryGetSession(request, out var session) == false)
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            RequestArgument requestArgument;
            try
            {
                requestArgument = await AnonymousServer.ReadJsonAsync<RequestArgument>(request, "Invalid anonymous room exit request.");
                if ((requestArgument == null) || string.IsNullOrWhiteSpace(requestArgument.PlayerId) || string.IsNullOrWhiteSpace(requestArgument.RoomId))
                    throw new FormatException("Invalid anonymous room exit request.");
            }
            catch (FormatException)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            var roomIndex = roomState.Rooms.FindIndex(secret => string.Equals(secret.Room.Id, requestArgument.RoomId, StringComparison.Ordinal));
            if (roomIndex < 0)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            var room = roomState.Rooms[roomIndex].Room;
            if ((string.Equals(session.Account, requestArgument.PlayerId, StringComparison.Ordinal) == false) && (string.Equals(session.Account, room.HostId, StringComparison.Ordinal) == false))
            {
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            var players = room.Players ?? Array.Empty<AnonymousServerCreateRoom.PlayerData>();
            if (string.Equals(room.HostId, requestArgument.PlayerId, StringComparison.Ordinal))
            {
                roomState.RoomCodes.Remove(room.Code);
                roomState.Rooms.RemoveAt(roomIndex);
            }
            else
            {
                room.Players = players.Where(player => string.Equals(player.Id, requestArgument.PlayerId, StringComparison.Ordinal) == false).ToArray();
            }

            response.StatusCode = (int)HttpStatusCode.NoContent;
        }
    }
}
