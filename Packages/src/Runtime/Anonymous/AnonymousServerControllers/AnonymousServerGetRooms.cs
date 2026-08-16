using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerGetRooms
    {
        [Serializable]
        internal record ResponseArgument
        {
            public AnonymousServerRoom.RoomData[] Rooms;
        }

        internal static async Task<AnonymousServerResponse> RunAsync(AnonymousServerRoom.State roomState, CancellationToken cancellationToken)
        {
            return await AnonymousServerResponse.CreateAsync(AnonymousTransport.ResultCodeEnum.Success, new ResponseArgument()
            {
                Rooms = roomState.Rooms.Where(secret => secret.Room.IsPrivate == false).Select(secret => secret.Room with
                {
                    Fields = secret.Room.Fields.Where(field => field.Visibility == MyNetInterface.Field.VisibilityEnum.Public).ToArray(),
                    Players = secret.Room.Players.Select(player => player with { Fields = player.Fields.Where(field => field.Visibility == MyNetInterface.Field.VisibilityEnum.Public).ToArray() }).ToArray(),
                }).ToArray(),
            }, cancellationToken);
        }
    }
}
