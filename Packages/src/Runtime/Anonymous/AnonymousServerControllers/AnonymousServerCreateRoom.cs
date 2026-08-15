using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerCreateRoom
    {
        private static async Task<string> ReadContentAsync(HttpListenerRequest request)
        {
            if (request.HasEntityBody == false)
                throw new FormatException("Anonymous room request body is empty.");

            try
            {
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    return await reader.ReadToEndAsync();
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("Invalid anonymous room request encoding.", exception);
            }
        }

        internal static async Task RunAsync(HttpListenerRequest request, HttpListenerResponse response, AnonymousServerRoom.State state)
        {
            if (request.HttpMethod != "POST")
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            AnonymousNetRoomProtocol.CreateResponseArgument room;
            try
            {
                AnonymousNetRoomProtocol.RoomRequestArgument args;
                try
                {
                    args = JsonUtility.FromJson<AnonymousNetRoomProtocol.RoomRequestArgument>(await ReadContentAsync(request));
                }
                catch (ArgumentException exception)
                {
                    throw new FormatException("Invalid anonymous room request.", exception);
                }

                if ((args == null) || string.IsNullOrEmpty(args.HostId) || (args.MaxPlayers < 1))
                    throw new FormatException("Invalid anonymous room request.");

                if (args.PlayerFields != null)
                    AnonymousNetRoomProtocol.ValidateFields(args.PlayerFields);
                if (args.RoomFields != null)
                    AnonymousNetRoomProtocol.ValidateFields(args.RoomFields);

                room = new AnonymousNetRoomProtocol.CreateResponseArgument()
                {
                    Code = CreateRoomCode(state),
                    Fields = args.RoomFields ?? Array.Empty<AnonymousNetRoomProtocol.FieldData>(),
                    HasPassword = string.IsNullOrEmpty(args.Password) == false,
                    HostId = args.HostId,
                    Id = Guid.NewGuid().ToString("N"),
                    IsLocked = args.IsLocked,
                    IsPrivate = args.IsPrivate,
                    MaxPlayers = args.MaxPlayers,
                    Players = new[]
                    {
                    new AnonymousNetRoomProtocol.PlayerData()
                    {
                        Fields = args.PlayerFields ?? Array.Empty<AnonymousNetRoomProtocol.FieldData>(),
                        Id = args.HostId,
                        IsHost = true,
                        Nickname = args.PlayerNickname,
                    },
                },
                    Title = args.Title,
                };

                state.Rooms.Add(new AnonymousServerRoom.RoomSecret(args.Password, room));
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
