using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerCreateRoom
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

            AnonymousNetRoomProtocol.CreateResponseArgument room;
            try
            {
                AnonymousNetRoomProtocol.RoomRequestArgument args;
                try
                {
                    args = JsonUtility.FromJson<AnonymousNetRoomProtocol.RoomRequestArgument>(await AnonymousServer.ReadContentAsync(request));
                }
                catch (ArgumentException exception)
                {
                    throw new FormatException("Invalid anonymous room request.", exception);
                }

                if ((args == null) || (args.MaxPlayers < 1))
                    throw new FormatException("Invalid anonymous room request.");

                if (args.PlayerFields != null)
                    AnonymousNetRoomProtocol.ValidateFields(args.PlayerFields);
                if (args.RoomFields != null)
                    AnonymousNetRoomProtocol.ValidateFields(args.RoomFields);

                room = new AnonymousNetRoomProtocol.CreateResponseArgument()
                {
                    Code = CreateRoomCode(roomState),
                    Fields = args.RoomFields ?? Array.Empty<AnonymousNetRoomProtocol.FieldData>(),
                    HasPassword = string.IsNullOrEmpty(args.Password) == false,
                    HostId = session.Account,
                    Id = Guid.NewGuid().ToString("N"),
                    IsLocked = args.IsLocked,
                    IsPrivate = args.IsPrivate,
                    MaxPlayers = args.MaxPlayers,
                    Players = new[]
                    {
                    new AnonymousNetRoomProtocol.PlayerData()
                    {
                        Fields = args.PlayerFields ?? Array.Empty<AnonymousNetRoomProtocol.FieldData>(),
                        Id = session.Account,
                        IsHost = true,
                        Nickname = args.PlayerNickname,
                    },
                },
                    Title = args.Title,
                };

                roomState.Rooms.Add(new AnonymousServerRoom.RoomSecret(args.Password, room));
            }
            catch (FormatException)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            var responseContent = JsonUtility.ToJson(room);
            var responseData = Encoding.UTF8.GetBytes(responseContent);

            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = responseData.Length;
            response.ContentType = "application/json; charset=utf-8";
            response.StatusCode = (int)HttpStatusCode.Created;

            await response.OutputStream.WriteAsync(responseData, 0, responseData.Length);

            static string CreateRoomCode(AnonymousServerRoom.State state)
            {
                string code;
                do
                {
                    code = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
                }
                while (state.RoomCodes.Add(code) == false);

                return code;
            }
        }
    }
}
