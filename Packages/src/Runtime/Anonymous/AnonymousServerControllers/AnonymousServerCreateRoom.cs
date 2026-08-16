using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerCreateRoom
    {
        [Serializable]
        internal record FieldData
        {
            public string Key;
            public string Value;
            public MyNetInterface.Field.VisibilityEnum Visibility;
        }

        [Serializable]
        internal record PlayerData
        {
            public FieldData[] Fields;
            public string Id;
            public bool IsHost;
            public string Nickname;
        }

        [Serializable]
        internal record RequestArgument
        {
            public bool IsLocked;
            public bool IsPrivate;
            public int MaxPlayers;
            public string Password;
            public FieldData[] PlayerFields;
            public string PlayerNickname;
            public FieldData[] RoomFields;
            public string Title;
        }

        [Serializable]
        internal record ResponseArgument
        {
            public string Code;
            public FieldData[] Fields;
            public bool HasPassword;
            public string HostId;
            public string Id;
            public bool IsLocked;
            public bool IsPrivate;
            public int MaxPlayers;
            public PlayerData[] Players;
            public string Title;
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

            ResponseArgument room;
            try
            {
                RequestArgument args;
                try
                {
                    args = JsonUtility.FromJson<RequestArgument>(await AnonymousServer.ReadContentAsync(request));
                }
                catch (ArgumentException exception)
                {
                    throw new FormatException("Invalid anonymous room request.", exception);
                }

                if ((args == null) || (args.MaxPlayers < 1))
                    throw new FormatException("Invalid anonymous room request.");

                if (args.PlayerFields != null)
                    ValidateFields(args.PlayerFields);
                if (args.RoomFields != null)
                    ValidateFields(args.RoomFields);

                room = new ResponseArgument()
                {
                    Code = CreateRoomCode(roomState),
                    Fields = args.RoomFields ?? Array.Empty<FieldData>(),
                    HasPassword = string.IsNullOrEmpty(args.Password) == false,
                    HostId = session.Account,
                    Id = Guid.NewGuid().ToString("N"),
                    IsLocked = args.IsLocked,
                    IsPrivate = args.IsPrivate,
                    MaxPlayers = args.MaxPlayers,
                    Players = new[]
                    {
                        new PlayerData()
                        {
                            Fields = args.PlayerFields ?? Array.Empty<FieldData>(),
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

        internal static void ValidateFields(FieldData[] fields)
        {
            foreach (var field in fields)
            {
                if ((field == null) || (Enum.IsDefined(typeof(MyNetInterface.Field.VisibilityEnum), field.Visibility) == false))
                    throw new FormatException("Invalid anonymous field data.");
            }
        }
    }
}
