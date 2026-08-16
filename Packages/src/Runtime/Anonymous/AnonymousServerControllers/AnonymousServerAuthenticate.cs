using System;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerAuthenticate
    {
        [Serializable]
        internal record RequestArgument
        {
            public string Account;
            public string Nickname;
        }

        internal static async Task<AnonymousServerSession> RunAsync(string content, CancellationToken cancellationToken)
        {
            var requestArgument = await AnonymousServer.ReadJsonAsync<RequestArgument>(content, "Invalid anonymous authentication request.", cancellationToken);
            if ((requestArgument == null) || string.IsNullOrEmpty(requestArgument.Account) || string.IsNullOrEmpty(requestArgument.Nickname))
                throw new FormatException("Invalid anonymous authentication request.");

            return new AnonymousServerSession(requestArgument.Account, requestArgument.Nickname);
        }
    }
}
