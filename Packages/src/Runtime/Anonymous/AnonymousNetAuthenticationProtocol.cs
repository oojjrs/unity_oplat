using System;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal static class AnonymousNetAuthenticationProtocol
    {
        internal const string SessionHeader = "X-Oplat-Session";

        [Serializable]
        internal record RequestArgument
        {
            public string Account;
            public string Nickname;

            internal RequestArgument(string account, string nickname)
            {
                Account = account;
                Nickname = nickname;
            }

            internal string ToJson()
            {
                return JsonUtility.ToJson(this);
            }
        }

        [Serializable]
        internal record ResponseArgument
        {
            public string Token;
        }
    }
}
