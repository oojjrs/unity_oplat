using System.Linq;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerGetRooms
    {
        public record ResponseArgument
        {
            public AnonymousServerRoom.RoomData[] Rooms { get; set; }
        }

        internal static async Task<AnonymousServerResponse> RunAsync(AnonymousServerRoom.State roomState)
        {
            return await AnonymousServerResponse.CreateAsync(AnonymousTransport.ResultCodeEnum.Success, new ResponseArgument()
            {
                Rooms = roomState.Rooms.Where(secret => secret.Room.IsPrivate == false).Select(secret => secret.Room with
                {
                    Fields = secret.Room.Fields.Where(field => field.Visibility == MyNetInterface.Field.VisibilityEnum.Public).ToArray(),
                    Players = secret.Room.Players.Select(player => player with { Fields = player.Fields.Where(field => field.Visibility == MyNetInterface.Field.VisibilityEnum.Public).ToArray() }).ToArray(),
                }).ToArray(),
            });
        }
    }
}
