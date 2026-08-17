using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerAddResponse
    {
        internal static async Task RunAsync(byte[] content, AnonymousServerRoom.State roomState, IReadOnlyDictionary<string, AnonymousServerSession> sessions, AnonymousServerSession session)
        {
            if (session == null)
                return;

            var room = roomState.Rooms.Find(secret => secret.Room.HostId == session.Account);
            if (room == null)
                return;

            var response = await AnonymousServer.DeserializeAsync<MyNetResponse>(content);
            if (response == null)
                throw new FormatException("Invalid anonymous host response.");

            var players = room.Room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>();
            foreach (var player in players)
            {
                if (player.Id == room.Room.HostId)
                    continue;

                if (sessions.TryGetValue(player.Id, out var playerSession))
                    playerSession.Messages.Send(AnonymousTransport.Message.CreateHostResponse(content));
            }
        }
    }
}
