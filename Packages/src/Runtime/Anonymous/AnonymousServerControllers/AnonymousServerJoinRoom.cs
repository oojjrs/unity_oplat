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

            RequestArgument requestArgument;
            try
            {
                requestArgument = await AnonymousServer.ReadJsonAsync<RequestArgument>(request, "Invalid anonymous room join request.");

                if ((requestArgument == null) || (string.IsNullOrWhiteSpace(requestArgument.RoomId) && string.IsNullOrWhiteSpace(requestArgument.Code)))
                    throw new FormatException("Invalid anonymous room join request.");

                if (requestArgument.PlayerFields != null)
                    AnonymousServerCreateRoom.ValidateFields(requestArgument.PlayerFields);
            }
            catch (FormatException)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            var roomId = requestArgument.RoomId?.Trim();
            var code = requestArgument.Code?.Trim();
            AnonymousServerRoom.RoomSecret secret;
            if (string.IsNullOrEmpty(roomId) == false)
                secret = roomState.Rooms.Find(value => value.Room.Id == roomId);
            else
                secret = roomState.Rooms.Find(value => string.Equals(value.Room.Code, code, StringComparison.OrdinalIgnoreCase));

            if (secret == null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            var room = secret.Room;
            var players = room.Players ?? Array.Empty<AnonymousServerCreateRoom.PlayerData>();
            if (players.Any(player => player.Id == session.Account) == false)
            {
                if (room.IsLocked || (string.IsNullOrEmpty(secret.Password) == false && secret.Password != requestArgument.Password))
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
                    Fields = requestArgument.PlayerFields ?? Array.Empty<AnonymousServerCreateRoom.FieldData>(),
                    Id = session.Account,
                    IsHost = false,
                    Nickname = requestArgument.PlayerNickname,
                }).ToArray();
            }

            var responseContent = JsonUtility.ToJson(AnonymousServerRoom.GetMemberResponseArgument(room, session.Account));
            var responseData = Encoding.UTF8.GetBytes(responseContent);

            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = responseData.Length;
            response.ContentType = "application/json; charset=utf-8";
            response.StatusCode = (int)HttpStatusCode.OK;

            await response.OutputStream.WriteAsync(responseData, 0, responseData.Length);
        }
    }
}
