using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerUpdatePlayer
    {
        public record RequestArgument
        {
            public AnonymousServerRoom.FieldData[] PlayerFields { get; set; }
            public string PlayerId { get; set; }
            public string RoomId { get; set; }
        }

        internal static async Task<AnonymousServerResponse> RunAsync(byte[] content, AnonymousServerRoom.State roomState, IReadOnlyDictionary<string, AnonymousServerSession> sessions, AnonymousServerSession session)
        {
            var requestArgument = await AnonymousServer.DeserializeAsync<RequestArgument>(content);
            if ((requestArgument == null) || string.IsNullOrWhiteSpace(requestArgument.PlayerId) || string.IsNullOrWhiteSpace(requestArgument.RoomId))
                throw new FormatException("Invalid anonymous player update request.");

            if (requestArgument.PlayerFields != null)
                AnonymousServerRoom.FieldData.Validate(requestArgument.PlayerFields);

            var playerId = requestArgument.PlayerId.Trim();
            var roomId = requestArgument.RoomId.Trim();
            var secret = roomState.Rooms.Find(value => value.Room.Id == roomId);
            if (secret == null)
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.NotFound);

            if (session.Account != playerId)
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

            var player = Array.Find(secret.Room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>(), value => value.Id == playerId);
            if (player == null)
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

            player.Fields = AnonymousServerRoom.FieldData.Merge(player.Fields, requestArgument.PlayerFields);
            foreach (var roomPlayer in secret.Room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>())
            {
                if ((roomPlayer.Id == session.Account) || (sessions.TryGetValue(roomPlayer.Id, out var playerSession) == false))
                    continue;

                var memberRoom = secret.Room.GetMemberResponseArgument(roomPlayer.Id);
                var memberPlayer = Array.Find(memberRoom.Players, value => value.Id == playerId);
                var memberResponse = await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, memberPlayer);
                playerSession.Messages.Send(AnonymousTransport.Message.CreatePlayerUpdated(memberResponse.Content));
            }

            var requesterRoom = secret.Room.GetMemberResponseArgument(session.Account);
            return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, Array.Find(requesterRoom.Players, value => value.Id == playerId));
        }
    }
}
