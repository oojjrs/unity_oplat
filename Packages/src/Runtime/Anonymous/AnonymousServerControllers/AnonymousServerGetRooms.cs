using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerGetRooms
    {
        internal static async Task RunAsync(HttpListenerRequest request, HttpListenerResponse response, AnonymousServerRoom.State roomState, AnonymousServerSession.State sessionState)
        {
            if (request.HttpMethod != "GET")
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            if (sessionState.TryGetSession(request, out _) == false)
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            var responseContent = JsonUtility.ToJson(new AnonymousNetRoomProtocol.RoomsData()
            {
                Rooms = roomState.Rooms.Where(secret => secret.Room.IsPrivate == false).Select(secret => secret.Room with
                {
                    Fields = secret.Room.Fields.Where(field => field.Visibility == MyNetInterface.Field.VisibilityEnum.Public).ToArray(),
                    Players = secret.Room.Players.Select(player => player with { Fields = player.Fields.Where(field => field.Visibility == MyNetInterface.Field.VisibilityEnum.Public).ToArray() }).ToArray(),
                }).ToArray(),
            });
            var responseData = Encoding.UTF8.GetBytes(responseContent);

            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = responseData.Length;
            response.ContentType = "application/json; charset=utf-8";
            response.StatusCode = (int)HttpStatusCode.OK;

            await response.OutputStream.WriteAsync(responseData, 0, responseData.Length);
        }
    }
}
