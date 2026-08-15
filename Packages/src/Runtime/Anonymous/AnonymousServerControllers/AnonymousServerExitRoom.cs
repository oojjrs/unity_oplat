using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerExitRoom
    {
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

            AnonymousNetRoomProtocol.ExitRequestArgument args;
            try
            {
                try
                {
                    args = JsonUtility.FromJson<AnonymousNetRoomProtocol.ExitRequestArgument>(await AnonymousServer.ReadContentAsync(request));
                }
                catch (ArgumentException exception)
                {
                    throw new FormatException("Invalid anonymous room exit request.", exception);
                }

                if ((args == null) || string.IsNullOrWhiteSpace(args.PlayerId) || string.IsNullOrWhiteSpace(args.RoomId))
                    throw new FormatException("Invalid anonymous room exit request.");
            }
            catch (FormatException)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            var roomIndex = roomState.Rooms.FindIndex(secret => string.Equals(secret.Room.Id, args.RoomId, StringComparison.Ordinal));
            if (roomIndex < 0)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            var room = roomState.Rooms[roomIndex].Room;
            if ((string.Equals(session.Account, args.PlayerId, StringComparison.Ordinal) == false) && (string.Equals(session.Account, room.HostId, StringComparison.Ordinal) == false))
            {
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            var players = room.Players ?? Array.Empty<AnonymousNetRoomProtocol.PlayerData>();
            if (string.Equals(room.HostId, args.PlayerId, StringComparison.Ordinal))
            {
                roomState.RoomCodes.Remove(room.Code);
                roomState.Rooms.RemoveAt(roomIndex);
            }
            else
            {
                room.Players = players.Where(player => string.Equals(player.Id, args.PlayerId, StringComparison.Ordinal) == false).ToArray();
            }

            response.StatusCode = (int)HttpStatusCode.NoContent;
        }
    }
}
