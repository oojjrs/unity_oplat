using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous.controllers
{
    internal static class AnonymousServerHealth
    {
        internal static async Task RunAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (request.HttpMethod != "GET")
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            var responseData = Encoding.UTF8.GetBytes(AnonymousServer.HealthResponse);
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = responseData.Length;
            response.ContentType = "text/plain; charset=utf-8";
            response.StatusCode = (int)HttpStatusCode.OK;

            await response.OutputStream.WriteAsync(responseData, 0, responseData.Length);
        }
    }
}
