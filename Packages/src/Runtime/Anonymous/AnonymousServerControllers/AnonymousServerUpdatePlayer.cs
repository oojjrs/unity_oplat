using System;
using System.Net;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerUpdatePlayer
    {
        [Serializable]
        internal record RequestArgument
        {
            public AnonymousServerCreateRoom.FieldData[] PlayerFields;
            public string PlayerId;
            public string RoomId;
        }

        internal static async Task RunAsync(HttpListenerRequest request, HttpListenerResponse response, AnonymousServerRoom.State roomState, AnonymousServerSession.State sessionState)
        {
            if (request.HttpMethod != "POST")
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            if (sessionState.TryGetSession(request, out var session) == false)
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            RequestArgument requestArgument;
            try
            {
                requestArgument = await AnonymousServer.ReadJsonAsync<RequestArgument>(request, "Invalid anonymous player update request.");
                if ((requestArgument == null) || string.IsNullOrWhiteSpace(requestArgument.PlayerId) || string.IsNullOrWhiteSpace(requestArgument.RoomId))
                    throw new FormatException("Invalid anonymous player update request.");

                if (requestArgument.PlayerFields != null)
                    AnonymousServerCreateRoom.ValidateFields(requestArgument.PlayerFields);
            }
            catch (FormatException)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            var playerId = requestArgument.PlayerId.Trim();
            var roomId = requestArgument.RoomId.Trim();
            var secret = roomState.Rooms.Find(value => value.Room.Id == roomId);
            if (secret == null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            if (session.Account != playerId)
            {
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            var player = Array.Find(secret.Room.Players ?? Array.Empty<AnonymousServerCreateRoom.PlayerData>(), value => value.Id == playerId);
            if (player == null)
            {
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            player.Fields = AnonymousServerRoom.MergeFields(player.Fields, requestArgument.PlayerFields);
            response.StatusCode = (int)HttpStatusCode.NoContent;
        }
    }
}
