using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerJoinRoom
    {
        [Serializable]
        internal record RequestArgument
        {
            public string Code;
            public string Password;
            public AnonymousServerCreateRoom.FieldData[] PlayerFields;
            public string PlayerNickname;
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

            RequestArgument args;
            try
            {
                try
                {
                    args = JsonUtility.FromJson<RequestArgument>(await AnonymousServer.ReadContentAsync(request));
                }
                catch (ArgumentException exception)
                {
                    throw new FormatException("Invalid anonymous room join request.", exception);
                }

                if ((args == null) || (string.IsNullOrWhiteSpace(args.RoomId) && string.IsNullOrWhiteSpace(args.Code)))
                    throw new FormatException("Invalid anonymous room join request.");

                if (args.PlayerFields != null)
                    AnonymousServerCreateRoom.ValidateFields(args.PlayerFields);
            }
            catch (FormatException)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            var roomId = args.RoomId?.Trim();
            var code = args.Code?.Trim();
            AnonymousServerRoom.RoomSecret secret;
            if (string.IsNullOrEmpty(roomId) == false)
            {
                secret = roomState.Rooms.Find(value => string.Equals(value.Room.Id, roomId, StringComparison.Ordinal));
            }
            else
            {
                secret = roomState.Rooms.Find(value => string.Equals(value.Room.Code, code, StringComparison.OrdinalIgnoreCase));
            }

            if (secret == null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            var room = secret.Room;
            var players = room.Players ?? Array.Empty<AnonymousServerCreateRoom.PlayerData>();
            if (players.Any(player => string.Equals(player.Id, session.Account, StringComparison.Ordinal)) == false)
            {
                if (room.IsLocked || (string.IsNullOrEmpty(secret.Password) == false && string.Equals(secret.Password, args.Password, StringComparison.Ordinal) == false))
                {
                    response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return;
                }

                if (players.Length >= room.MaxPlayers)
                {
                    response.StatusCode = (int)HttpStatusCode.Conflict;
                    return;
                }

                room.Players = players.Append(new AnonymousServerCreateRoom.PlayerData()
                {
                    Fields = args.PlayerFields ?? Array.Empty<AnonymousServerCreateRoom.FieldData>(),
                    Id = session.Account,
                    IsHost = false,
                    Nickname = args.PlayerNickname,
                }).ToArray();
            }

            var isHost = string.Equals(room.HostId, session.Account, StringComparison.Ordinal);
            var memberRoom = room with
            {
                Fields = (room.Fields ?? Array.Empty<AnonymousServerCreateRoom.FieldData>()).Where(field => (field.Visibility != MyNetInterface.Field.VisibilityEnum.Private) || isHost).ToArray(),
                Players = (room.Players ?? Array.Empty<AnonymousServerCreateRoom.PlayerData>()).Select(player => player with
                {
                    Fields = (player.Fields ?? Array.Empty<AnonymousServerCreateRoom.FieldData>()).Where(field => (field.Visibility != MyNetInterface.Field.VisibilityEnum.Private) || string.Equals(player.Id, session.Account, StringComparison.Ordinal)).ToArray(),
                }).ToArray(),
            };
            var responseContent = JsonUtility.ToJson(memberRoom);
            var responseData = Encoding.UTF8.GetBytes(responseContent);

            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = responseData.Length;
            response.ContentType = "application/json; charset=utf-8";
            response.StatusCode = (int)HttpStatusCode.OK;

            await response.OutputStream.WriteAsync(responseData, 0, responseData.Length);
        }
    }
}
