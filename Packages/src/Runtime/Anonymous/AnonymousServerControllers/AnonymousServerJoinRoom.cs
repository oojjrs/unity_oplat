using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerJoinRoom
    {
        [Serializable]
        internal record RequestArgument
        {
            public string Code;
            public string Password;
            public AnonymousServerRoom.FieldData[] PlayerFields;
            public string PlayerNickname;
            public string RoomId;
        }

        internal static async Task<AnonymousServerResponse> RunAsync(string content, AnonymousServerRoom.State roomState, AnonymousServerSession session, CancellationToken cancellationToken)
        {
            var requestArgument = await AnonymousServer.ReadJsonAsync<RequestArgument>(content, "Invalid anonymous room join request.", cancellationToken);
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
                return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.NotFound);

            var room = secret.Room;
            var players = room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>();
            if (players.Any(player => player.Id == session.Account) == false)
            {
                if (room.IsLocked || (string.IsNullOrEmpty(secret.Password) == false && secret.Password != requestArgument.Password))
                    return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Forbidden);

                if (players.Length >= room.MaxPlayers)
                    return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Conflict);

                room.Players = players.Append(new AnonymousServerRoom.PlayerData()
                {
                    Fields = requestArgument.PlayerFields ?? Array.Empty<AnonymousServerRoom.FieldData>(),
                    Id = session.Account,
                    IsHost = false,
                    Nickname = requestArgument.PlayerNickname,
                }).ToArray();
            }

            return await AnonymousServerResponse.CreateAsync(AnonymousTransport.ResultCodeEnum.Success, room.GetMemberResponseArgument(session.Account), cancellationToken);
        }
    }
}
