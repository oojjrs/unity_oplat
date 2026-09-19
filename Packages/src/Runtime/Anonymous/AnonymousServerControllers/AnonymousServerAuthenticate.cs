using System;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerAuthenticate
    {
        public record RequestArgument
        {
            public string Account { get; set; }
            public uint AppId { get; set; }
            public string Nickname { get; set; }
            public string ProjectKey { get; set; }
        }

        internal static async Task<AnonymousServerSession> RunAsync(byte[] content, AnonymousTransport.MessageQueue messages)
        {
            var requestArgument = await AnonymousServer.DeserializeAsync<RequestArgument>(content);
            if ((requestArgument == null) || string.IsNullOrEmpty(requestArgument.Account) || string.IsNullOrEmpty(requestArgument.Nickname) || string.IsNullOrEmpty(requestArgument.ProjectKey))
                throw new FormatException("Invalid anonymous authentication request.");

            return new AnonymousServerSession(requestArgument.Account, requestArgument.AppId, messages, requestArgument.Nickname, requestArgument.ProjectKey);
        }
    }
}
