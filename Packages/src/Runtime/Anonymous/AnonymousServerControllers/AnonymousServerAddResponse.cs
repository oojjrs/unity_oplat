using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerAddResponse
    {
        public record RequestArgument
        {
            public byte[] Content { get; set; }
            public string PlayerId { get; set; }
        }

        internal static async Task RunAsync(byte[] content, AnonymousServerRoom.State roomState, AnonymousServerSession session)
        {
            if (session == null)
                return;

            var room = roomState.Rooms.Find(secret => secret.Room.HostId == session.Account);
            if (room == null)
                return;

            var requestArgument = await AnonymousServer.DeserializeAsync<RequestArgument>(content);
            if ((requestArgument == null) || string.IsNullOrWhiteSpace(requestArgument.PlayerId) || (requestArgument.Content == null))
                throw new FormatException("Invalid anonymous host response.");

            var response = await AnonymousServer.DeserializeAsync<MyNetResponse>(requestArgument.Content);
            if (response == null)
                throw new FormatException("Invalid anonymous host response.");

            var players = room.Room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>();
            if (Array.Exists(players, player => (player.Id == requestArgument.PlayerId) && (player.Id != room.Room.HostId)) == false)
                return;

            if (room.Responses.TryGetValue(requestArgument.PlayerId, out var responses) == false)
            {
                responses = new Queue<MyNetResponse>();
                room.Responses.Add(requestArgument.PlayerId, responses);
            }

            responses.Enqueue(response);
        }
    }
}
