using System;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerExitChat
    {
        public sealed record RequestArgument
        {
            public string RoomId { get; set; }
        }

        internal static async Task<AnonymousServerResponse> RunAsync(byte[] content, AnonymousServerChat.State chatState, AnonymousServerSession session)
        {
            var argument = await AnonymousServer.DeserializeAsync<RequestArgument>(content);
            if ((argument == null) || string.IsNullOrWhiteSpace(argument.RoomId))
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.NotFound);

            chatState.Remove(session.Account, argument.RoomId);
            return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Success);
        }
    }
}
