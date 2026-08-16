using System;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerUpdatePlayer
    {
        [Serializable]
        internal record RequestArgument
        {
            public AnonymousServerRoom.FieldData[] PlayerFields;
            public string PlayerId;
            public string RoomId;
        }

        internal static async Task<AnonymousServerResponse> RunAsync(string content, AnonymousServerRoom.State roomState, AnonymousServerSession session, CancellationToken cancellationToken)
        {
            var requestArgument = await AnonymousServer.ReadJsonAsync<RequestArgument>(content, "Invalid anonymous player update request.", cancellationToken);
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
            return await AnonymousServerResponse.CreateAsync(AnonymousTransport.ResultCodeEnum.Success, secret.Room.GetMemberResponseArgument(session.Account), cancellationToken);
        }
    }
}
