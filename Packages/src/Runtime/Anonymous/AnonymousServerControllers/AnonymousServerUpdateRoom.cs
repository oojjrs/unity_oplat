using System;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerUpdateRoom
    {
        public record RequestArgument
        {
            public bool IsPrivate { get; set; }
            public AnonymousServerRoom.FieldData[] RoomFields { get; set; }
            public string RoomId { get; set; }
        }

        internal static AnonymousServerResponse Run(byte[] content, AnonymousServerRoom.State roomState, AnonymousServerSession session)
        {
            var requestArgument = AnonymousTransport.Deserialize<RequestArgument>(content);
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

            return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Success, room.GetMemberResponseArgument(session.Account));
        }
    }
}
