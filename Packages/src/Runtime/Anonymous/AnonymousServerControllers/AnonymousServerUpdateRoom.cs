using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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

        internal static async Task<AnonymousServerResponse> RunAsync(byte[] content, AnonymousServerRoom.State roomState, IReadOnlyDictionary<string, AnonymousServerSession> sessions, AnonymousServerSession session)
        {
            var requestArgument = await AnonymousServer.DeserializeAsync<RequestArgument>(content);
            if ((requestArgument == null) || string.IsNullOrWhiteSpace(requestArgument.RoomId))
                throw new FormatException("Invalid anonymous room update request.");

            if (requestArgument.RoomFields != null)
                AnonymousServerRoom.FieldData.Validate(requestArgument.RoomFields);

            var roomId = requestArgument.RoomId.Trim();
            var secret = roomState.Rooms.Find(value => value.Room.Id == roomId);
            if (secret == null)
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.NotFound);

            var room = secret.Room;
            if (session.Account != room.HostId)
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

            room.Fields = AnonymousServerRoom.FieldData.Merge(room.Fields, requestArgument.RoomFields);
            room.IsPrivate = requestArgument.IsPrivate;

            foreach (var player in room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>())
            {
                if ((player.Id == session.Account) || (sessions.TryGetValue(player.Id, out var playerSession) == false))
                    continue;

                var memberResponse = await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, room.GetMemberResponseArgument(player.Id));
                playerSession.Messages.Send(AnonymousTransport.Message.CreateRoomUpdated(memberResponse.Content));
            }

            return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, room.GetMemberResponseArgument(session.Account));
        }
    }
}
