using System;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerAuthenticate
    {
        public record RequestArgument
        {
            public string Account { get; set; }
            public string Nickname { get; set; }
        }

        internal static AnonymousServerSession Run(byte[] content)
        {
            var requestArgument = AnonymousTransport.Deserialize<RequestArgument>(content);
            if ((requestArgument == null) || string.IsNullOrEmpty(requestArgument.Account) || string.IsNullOrEmpty(requestArgument.Nickname))
                throw new FormatException("Invalid anonymous authentication request.");

            return new AnonymousServerSession(requestArgument.Account, requestArgument.Nickname);
        }
    }
}
