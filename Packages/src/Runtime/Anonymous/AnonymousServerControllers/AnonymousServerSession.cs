using System;
using System.Collections.Generic;
using System.Net;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerSession
    {
        internal sealed class Session
        {
            internal Session(string account, string nickname)
            {
                Account = account;
                Nickname = nickname;
            }

            internal string Account { get; }
            internal string Nickname { get; }
        }

        internal sealed class State
        {
            private readonly Dictionary<string, Session> _sessions = new();

            internal string Create(string account, string nickname)
            {
                var token = Guid.NewGuid().ToString("N");
                _sessions.Add(token, new Session(account, nickname));
                return token;
            }

            internal bool TryGetSession(HttpListenerRequest request, out Session session)
            {
                var token = request.Headers[AnonymousNetAuthenticationProtocol.SessionHeader];
                if (string.IsNullOrEmpty(token))
                {
                    session = null;
                    return false;
                }

                return _sessions.TryGetValue(token, out session);
            }
        }
    }
}
