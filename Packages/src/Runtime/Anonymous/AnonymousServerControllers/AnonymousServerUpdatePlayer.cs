using System;

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

        internal static AnonymousServerResponse Run(byte[] content, AnonymousServerRoom.State roomState, AnonymousServerSession session)
        {
            var requestArgument = AnonymousTransport.Deserialize<RequestArgument>(content);
            if ((requestArgument == null) || string.IsNullOrWhiteSpace(requestArgument.PlayerId) || string.IsNullOrWhiteSpace(requestArgument.RoomId))
                throw new FormatException("Invalid anonymous player update request.");

            if (requestArgument.PlayerFields != null)
                AnonymousServerRoom.FieldData.Validate(requestArgument.PlayerFields);

            var playerId = requestArgument.PlayerId.Trim();
            var roomId = requestArgument.RoomId.Trim();
            var secret = roomState.Rooms.Find(value => value.Room.Id == roomId);
            if (secret == null)
                return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.NotFound);

            if (session.Account != playerId)
                return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Forbidden);

            var player = Array.Find(secret.Room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>(), value => value.Id == playerId);
            if (player == null)
                return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Forbidden);

            player.Fields = AnonymousServerRoom.FieldData.Merge(player.Fields, requestArgument.PlayerFields);
            return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Success, secret.Room.GetMemberResponseArgument(session.Account));
        }
    }
}
