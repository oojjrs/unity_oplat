using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

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

        [Serializable]
        internal record ResponseArgument
        {
            public string Token;
        }

        internal const string SessionHeader = "X-Oplat-Session";

        internal static async Task RunAsync(HttpListenerRequest request, HttpListenerResponse response, AnonymousServerSession.State state)
        {
            if (request.HttpMethod != "POST")
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            ResponseArgument responseArgument;
            try
            {
                var requestArgument = await AnonymousServer.ReadJsonAsync<RequestArgument>(request, "Invalid anonymous authentication request.");
                if ((requestArgument == null) || string.IsNullOrEmpty(requestArgument.Account) || string.IsNullOrEmpty(requestArgument.Nickname))
                    throw new FormatException("Invalid anonymous authentication request.");

                responseArgument = new ResponseArgument
                {
                    Token = state.Create(requestArgument.Account, requestArgument.Nickname),
                };
            }
            catch (FormatException)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            var responseContent = JsonUtility.ToJson(responseArgument);
            var responseData = Encoding.UTF8.GetBytes(responseContent);

            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = responseData.Length;
            response.ContentType = "application/json; charset=utf-8";
            response.StatusCode = (int)HttpStatusCode.Created;

            await response.OutputStream.WriteAsync(responseData, 0, responseData.Length);
        }
    }
}
