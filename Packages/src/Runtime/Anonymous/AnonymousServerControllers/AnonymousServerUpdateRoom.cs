using System;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerUpdateRoom
    {
        [Serializable]
        internal record RequestArgument
        {
            public bool IsPrivate;
            public AnonymousServerRoom.FieldData[] RoomFields;
            public string RoomId;
        }

        internal static async Task<AnonymousServerResponse> RunAsync(string content, AnonymousServerRoom.State roomState, AnonymousServerSession session, CancellationToken cancellationToken)
        {
            var requestArgument = await AnonymousServer.ReadJsonAsync<RequestArgument>(content, "Invalid anonymous room update request.", cancellationToken);
            if ((requestArgument == null) || string.IsNullOrWhiteSpace(requestArgument.RoomId))
                throw new FormatException("Invalid anonymous room update request.");

            if (requestArgument.RoomFields != null)
                AnonymousServerRoom.FieldData.Validate(requestArgument.RoomFields);

            var roomId = requestArgument.RoomId.Trim();
            var secret = roomState.Rooms.Find(value => value.Room.Id == roomId);
            if (secret == null)
                return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.NotFound);

            var room = secret.Room;
            if (session.Account != room.HostId)
                return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Forbidden);

            room.Fields = AnonymousServerRoom.FieldData.Merge(room.Fields, requestArgument.RoomFields);
            room.IsPrivate = requestArgument.IsPrivate;

            return await AnonymousServerResponse.CreateAsync(AnonymousTransport.ResultCodeEnum.Success, room.GetMemberResponseArgument(session.Account), cancellationToken);
        }
    }
}
