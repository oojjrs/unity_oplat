using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousServer
    {
        internal const string Address = "http://127.0.0.1:45831/";

        private const string HealthAddress = Address + "health";
        private const string HealthPath = "/health";
        private const string HealthResponse = "oojjrs.oplat.anonymous/2";
        private const string RoomsAddress = Address + "rooms";
        private const string RoomsPath = "/rooms";
        private const string RoomsResponse = "[]";

        private readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(1) };
        private readonly HttpListener Listener = new();
        private readonly SemaphoreSlim StartSemaphore = new(1, 1);
        private readonly object StateLock = new();

        private bool _isOwner;
        private bool _isShutdown;

        internal AnonymousServer()
        {
            Listener.Prefixes.Add(Address);
        }

        private static async Task RespondAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            try
            {
                if (request.HttpMethod != "GET")
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                string responseContent;
                var requestPath = request.Url.AbsolutePath;
                if (requestPath == HealthPath)
                {
                    response.ContentType = "text/plain; charset=utf-8";
                    responseContent = HealthResponse;
                }
                else if (requestPath == RoomsPath)
                {
                    response.ContentType = "application/json; charset=utf-8";
                    responseContent = RoomsResponse;
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                var responseData = Encoding.UTF8.GetBytes(responseContent);
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = responseData.Length;
                response.StatusCode = (int)HttpStatusCode.OK;

                await response.OutputStream.WriteAsync(responseData, 0, responseData.Length).ConfigureAwait(false);
            }
            finally
            {
                response.Close();
            }
        }

        internal async Task<MyNetRoomInterface[]> GetRoomsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var response = await Client.GetAsync(RoomsAddress, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                cancellationToken.ThrowIfCancellationRequested();

                if (string.Equals(responseContent, RoomsResponse, StringComparison.Ordinal) == false)
                    throw new FormatException("Invalid anonymous rooms response.");

                return Array.Empty<MyNetRoomInterface>();
            }
        }

        private async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            try
            {
                using (var response = await Client.GetAsync(HealthAddress, cancellationToken))
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                        return false;

                    return string.Equals(await response.Content.ReadAsStringAsync(), HealthResponse, StringComparison.Ordinal);
                }
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested == false)
            {
                return false;
            }
        }

        private async void ListenAsync()
        {
            try
            {
                while (Listener.IsListening)
                {
                    var context = await Listener.GetContextAsync().ConfigureAwait(false);

                    await RespondAsync(context).ConfigureAwait(false);
                }
            }
            catch (HttpListenerException) when (Listener.IsListening == false)
            {
            }
            catch (ObjectDisposedException) when (Listener.IsListening == false)
            {
            }
        }

        internal async Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await StartSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    lock (StateLock)
                    {
                        if (_isShutdown)
                            throw new ObjectDisposedException(nameof(AnonymousServer));

                        if (_isOwner)
                            return;

                        Listener.Start();
                        _isOwner = true;

                        ListenAsync();
                    }
                }
                catch (HttpListenerException)
                {
                    if (await IsAvailableAsync(cancellationToken))
                        return;

                    throw;
                }
            }
            finally
            {
                StartSemaphore.Release();
            }
        }

        internal void Shutdown()
        {
            lock (StateLock)
            {
                if (_isShutdown == false)
                {
                    _isShutdown = true;
                    if (_isOwner)
                    {
                        try
                        {
                            Listener.Stop();
                        }
                        finally
                        {
                            _isOwner = false;
                        }
                    }
                }
            }
        }
    }
}
