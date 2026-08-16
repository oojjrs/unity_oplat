using System;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerGetRequests
    {
        public record RequestData
        {
            public byte[] Content { get; set; }
        }

        public record ResponseArgument
        {
            public RequestData[] Requests { get; set; }
        }

        internal static async Task<AnonymousServerResponse> RunAsync(AnonymousServerRoom.State roomState, AnonymousServerSession session)
        {
            var room = roomState.Rooms.Find(secret => secret.Room.HostId == session.Account);
            if (room == null)
            {
                return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, new ResponseArgument()
                {
                    Requests = Array.Empty<RequestData>(),
                });
            }

            var requests = new RequestData[room.Requests.Count];
            for (var index = 0; index < requests.Length; ++index)
            {
                requests[index] = new RequestData()
                {
                    Content = MyNetSerializer.Serialize(room.Requests.Dequeue()),
                };
            }

            return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, new ResponseArgument()
            {
                Requests = requests,
            });
        }
    }
}
