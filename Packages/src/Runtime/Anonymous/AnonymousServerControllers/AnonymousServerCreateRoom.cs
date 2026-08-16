using System;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerCreateRoom
    {
        [Serializable]
        internal record RequestArgument
        {
            public bool IsLocked;
            public bool IsPrivate;
            public int MaxPlayers;
            public string Password;
            public AnonymousServerRoom.FieldData[] PlayerFields;
            public string PlayerNickname;
            public AnonymousServerRoom.FieldData[] RoomFields;
            public string Title;
        }

        internal static async Task<AnonymousServerResponse> RunAsync(string content, AnonymousServerRoom.State roomState, AnonymousServerSession session, CancellationToken cancellationToken)
        {
            var requestArgument = await AnonymousServer.ReadJsonAsync<RequestArgument>(content, "Invalid anonymous room request.", cancellationToken);
            if ((requestArgument == null) || (requestArgument.MaxPlayers < 1))
                throw new FormatException("Invalid anonymous room request.");

            if (requestArgument.PlayerFields != null)
                AnonymousServerRoom.FieldData.Validate(requestArgument.PlayerFields);
            if (requestArgument.RoomFields != null)
                AnonymousServerRoom.FieldData.Validate(requestArgument.RoomFields);

            var responseArgument = new AnonymousServerRoom.RoomData()
            {
                Code = CreateRoomCode(roomState),
                Fields = requestArgument.RoomFields ?? Array.Empty<AnonymousServerRoom.FieldData>(),
                HasPassword = string.IsNullOrEmpty(requestArgument.Password) == false,
                HostId = session.Account,
                Id = Guid.NewGuid().ToString("N"),
                IsLocked = requestArgument.IsLocked,
                IsPrivate = requestArgument.IsPrivate,
                MaxPlayers = requestArgument.MaxPlayers,
                Players = new[]
                {
                        new AnonymousServerRoom.PlayerData()
                        {
                            Fields = requestArgument.PlayerFields ?? Array.Empty<AnonymousServerRoom.FieldData>(),
                            Id = session.Account,
                            IsHost = true,
                            Nickname = requestArgument.PlayerNickname,
                        },
                    },
                Title = requestArgument.Title,
            };

            roomState.Rooms.Add(new AnonymousServerRoom.RoomSecret(requestArgument.Password, responseArgument));
            return await AnonymousServerResponse.CreateAsync(AnonymousTransport.ResultCodeEnum.Success, responseArgument, cancellationToken);

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
