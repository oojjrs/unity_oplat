using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerUpdateRoom
    {
        [Serializable]
        internal record RequestArgument
        {
            public bool IsPrivate;
            public AnonymousServerCreateRoom.FieldData[] RoomFields;
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
                requestArgument = await AnonymousServer.ReadJsonAsync<RequestArgument>(request, "Invalid anonymous room update request.");
                if ((requestArgument == null) || string.IsNullOrWhiteSpace(requestArgument.RoomId))
                    throw new FormatException("Invalid anonymous room update request.");

                if (requestArgument.RoomFields != null)
                    AnonymousServerCreateRoom.ValidateFields(requestArgument.RoomFields);
            }
            catch (FormatException)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            var roomId = requestArgument.RoomId.Trim();
            var secret = roomState.Rooms.Find(value => string.Equals(value.Room.Id, roomId, StringComparison.Ordinal));
            if (secret == null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            var room = secret.Room;
            if (string.Equals(session.Account, room.HostId, StringComparison.Ordinal) == false)
            {
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            var updatedFields = requestArgument.RoomFields ?? Array.Empty<AnonymousServerCreateRoom.FieldData>();
            var roomFields = room.Fields ?? Array.Empty<AnonymousServerCreateRoom.FieldData>();
            if (updatedFields.Length > 0)
                roomFields = roomFields.Where(field => updatedFields.Any(updatedField => string.Equals(updatedField.Key, field.Key, StringComparison.Ordinal)) == false).Concat(updatedFields).ToArray();

            room.IsPrivate = requestArgument.IsPrivate;
            room.Fields = roomFields;

            var responseContent = JsonUtility.ToJson(AnonymousServerRoom.GetMemberResponseArgument(room, session.Account));
            var responseData = Encoding.UTF8.GetBytes(responseContent);

            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = responseData.Length;
            response.ContentType = "application/json; charset=utf-8";
            response.StatusCode = (int)HttpStatusCode.OK;

            await response.OutputStream.WriteAsync(responseData, 0, responseData.Length);
        }
    }
}
