using System;
using System.Linq;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerJoinChat
    {
        public sealed record RequestArgument
        {
            public string RoomId { get; set; }
        }

        internal static async Task<AnonymousServerResponse> RunAsync(byte[] content, AnonymousServerChat.State chatState, AnonymousServerRoom.State roomState, AnonymousServerSession session)
        {
            var argument = await AnonymousServer.DeserializeAsync<RequestArgument>(content);
            if ((argument == null) || string.IsNullOrWhiteSpace(argument.RoomId))
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.NotFound);

            var room = roomState.Rooms.FirstOrDefault(value => value.Room.Id == argument.RoomId);
            if (room == null)
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.NotFound);

            if ((room.Room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>()).Any(player => player.Id == session.Account) == false)
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

            chatState.Join(session.Account, argument.RoomId);
            return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Success);
        }
    }
}
