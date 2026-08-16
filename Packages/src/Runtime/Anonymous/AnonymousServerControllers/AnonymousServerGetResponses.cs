using System;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerGetResponses
    {
        public record ResponseData
        {
            public byte[] Content { get; set; }
        }

        public record ResponseArgument
        {
            public ResponseData[] Responses { get; set; }
        }

        internal static async Task<AnonymousServerResponse> RunAsync(AnonymousServerRoom.State roomState, AnonymousServerSession session)
        {
            var room = roomState.Rooms.Find(secret => (secret.Room.HostId != session.Account) && Array.Exists(secret.Room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>(), player => player.Id == session.Account));
            if ((room == null) || (room.Responses.TryGetValue(session.Account, out var queuedResponses) == false))
                return await CreateResponseAsync(Array.Empty<ResponseData>());

            room.Responses.Remove(session.Account);
            var responses = new ResponseData[queuedResponses.Count];
            for (var index = 0; index < responses.Length; ++index)
            {
                responses[index] = new ResponseData()
                {
                    Content = MyNetSerializer.Serialize(queuedResponses.Dequeue()),
                };
            }

            return await CreateResponseAsync(responses);
        }

        private static Task<AnonymousServerResponse> CreateResponseAsync(ResponseData[] responses)
        {
            return AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, new ResponseArgument()
            {
                Responses = responses,
            });
        }
    }
}
