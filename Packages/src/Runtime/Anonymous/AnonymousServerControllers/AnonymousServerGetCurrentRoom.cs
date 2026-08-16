using System;
using System.Linq;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerGetCurrentRoom
    {
        internal static async Task<AnonymousServerResponse> RunAsync(AnonymousServerRoom.State roomState, AnonymousServerSession session)
        {
            var rooms = roomState.Rooms.Where(secret => (secret.Room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>()).Any(player => player.Id == session.Account)).Take(2).ToArray();
            if (rooms.Length == 0)
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.NotFound);

            if (rooms.Length > 1)
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Conflict);

            return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, rooms[0].Room.GetMemberResponseArgument(session.Account));
        }
    }
}
