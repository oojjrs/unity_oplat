using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerJoinRoom
    {
        public record RequestArgument
        {
            public string Code { get; set; }
            public string Password { get; set; }
            public AnonymousServerRoom.FieldData[] PlayerFields { get; set; }
            public string PlayerNickname { get; set; }
            public string RoomId { get; set; }
        }

        internal static async Task<AnonymousServerResponse> RunAsync(byte[] content, AnonymousServerRoom.State roomState, IReadOnlyDictionary<string, AnonymousServerSession> sessions, AnonymousServerSession session)
        {
            var requestArgument = await AnonymousServer.DeserializeAsync<RequestArgument>(content);
            if ((requestArgument == null) || (string.IsNullOrWhiteSpace(requestArgument.RoomId) && string.IsNullOrWhiteSpace(requestArgument.Code)))
                throw new FormatException("Invalid anonymous room join request.");

            if (requestArgument.PlayerFields != null)
                AnonymousServerRoom.FieldData.Validate(requestArgument.PlayerFields);

            var roomId = requestArgument.RoomId?.Trim();
            var code = requestArgument.Code?.Trim();
            AnonymousServerRoom.RoomSecret secret;
            if (string.IsNullOrEmpty(roomId) == false)
                secret = roomState.Rooms.Find(value => value.Room.Id == roomId);
            else
                secret = roomState.Rooms.Find(value => string.Equals(value.Room.Code, code, StringComparison.OrdinalIgnoreCase));

            if (secret == null)
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.NotFound);

            var room = secret.Room;
            var players = room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>();
            if (players.Any(player => player.Id == session.Account) == false)
            {
                if (room.IsLocked || (string.IsNullOrEmpty(secret.Password) == false && secret.Password != requestArgument.Password))
                    return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

                if (players.Length >= room.MaxPlayers)
                    return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Conflict);

                room.Players = players.Append(new AnonymousServerRoom.PlayerData()
                {
                    Fields = requestArgument.PlayerFields ?? Array.Empty<AnonymousServerRoom.FieldData>(),
                    Id = session.Account,
                    IsHost = false,
                    Nickname = requestArgument.PlayerNickname,
                }).ToArray();
                await AnonymousServerRoom.NotifyUpdatedAsync(room, sessions, session.Account);
            }

            return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, room.GetMemberResponseArgument(session.Account));
        }
    }
}
