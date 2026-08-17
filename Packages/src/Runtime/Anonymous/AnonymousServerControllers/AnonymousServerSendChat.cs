using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerSendChat
    {
        public sealed record RequestArgument
        {
            public string Message { get; set; }
            public string RoomId { get; set; }
        }

        internal static async Task<AnonymousServerResponse> RunAsync(byte[] content, AnonymousServerChat.State chatState, AnonymousServerRoom.State roomState, IReadOnlyDictionary<string, AnonymousServerSession> sessions, AnonymousServerSession session)
        {
            var argument = await AnonymousServer.DeserializeAsync<RequestArgument>(content);
            if ((argument == null) || string.IsNullOrWhiteSpace(argument.RoomId))
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.NotFound);

            if (string.IsNullOrWhiteSpace(argument.Message) || (Encoding.UTF8.GetByteCount(argument.Message) > AnonymousNetChatService.MessageByteCountMax))
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

            var room = roomState.Rooms.FirstOrDefault(value => value.Room.Id == argument.RoomId);
            if (room == null)
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.NotFound);

            var roomPlayerIds = new HashSet<string>((room.Room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>()).Select(player => player.Id));
            if ((roomPlayerIds.Contains(session.Account) == false) || (chatState.Contains(session.Account, argument.RoomId) == false))
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

            var message = MyNetSerializer.Serialize(new AnonymousServerChat.MessageData()
            {
                Message = argument.Message,
                PlayerId = session.Account,
                RoomId = argument.RoomId,
            });
            foreach (var playerId in chatState.GetPlayers(argument.RoomId).ToArray())
            {
                if (roomPlayerIds.Contains(playerId) && sessions.TryGetValue(playerId, out var targetSession))
                    targetSession.Messages.Send(AnonymousTransport.Message.CreateChatReceived(message));
                else
                    chatState.Remove(playerId, argument.RoomId);
            }

            return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Success);
        }
    }
}
