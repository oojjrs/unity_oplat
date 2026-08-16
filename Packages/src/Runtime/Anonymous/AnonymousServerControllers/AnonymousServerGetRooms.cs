using System.Linq;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerGetRooms
    {
        public record ResponseArgument
        {
            public AnonymousServerRoom.RoomData[] Rooms { get; set; }
        }

        internal static AnonymousServerResponse Run(AnonymousServerRoom.State roomState)
        {
            return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Success, new ResponseArgument()
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
