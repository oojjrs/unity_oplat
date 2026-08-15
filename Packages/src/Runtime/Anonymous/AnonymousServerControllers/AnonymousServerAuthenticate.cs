using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerAuthenticate
    {
        internal static async Task RunAsync(HttpListenerRequest request, HttpListenerResponse response, AnonymousServerSession.State state)
        {
            if (request.HttpMethod != "POST")
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            AnonymousNetAuthenticationProtocol.ResponseArgument responseArgument;
            try
            {
                AnonymousNetAuthenticationProtocol.RequestArgument requestArgument;
                try
                {
                    requestArgument = JsonUtility.FromJson<AnonymousNetAuthenticationProtocol.RequestArgument>(await AnonymousServer.ReadContentAsync(request));
                }
                catch (ArgumentException exception)
                {
                    throw new FormatException("Invalid anonymous authentication request.", exception);
                }

                if ((requestArgument == null) || string.IsNullOrEmpty(requestArgument.Account) || string.IsNullOrEmpty(requestArgument.Nickname))
                    throw new FormatException("Invalid anonymous authentication request.");

                responseArgument = new AnonymousNetAuthenticationProtocol.ResponseArgument
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
