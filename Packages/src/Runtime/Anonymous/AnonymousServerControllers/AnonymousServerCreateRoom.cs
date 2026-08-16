using System;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerCreateRoom
    {
        public record RequestArgument
        {
            public bool IsLocked { get; set; }
            public bool IsPrivate { get; set; }
            public int MaxPlayers { get; set; }
            public string Password { get; set; }
            public AnonymousServerRoom.FieldData[] PlayerFields { get; set; }
            public string PlayerNickname { get; set; }
            public AnonymousServerRoom.FieldData[] RoomFields { get; set; }
            public string Title { get; set; }
        }

        internal static AnonymousServerResponse Run(byte[] content, AnonymousServerRoom.State roomState, AnonymousServerSession session)
        {
            var requestArgument = AnonymousTransport.Deserialize<RequestArgument>(content);
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
            return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Success, responseArgument);

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
