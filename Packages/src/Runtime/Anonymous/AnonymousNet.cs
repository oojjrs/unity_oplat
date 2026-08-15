using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNet : MyNetInterface
    {
        private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(1) };
        private readonly CancellationTokenSource _lifetimeCancellationSource = new();
        private readonly AnonymousNetLobbyService _lobby;
        private readonly AnonymousNetRoomService _room;
        private readonly AnonymousServer _server = new();

        internal AnonymousNet()
        {
            _lobby = new(_client, _lifetimeCancellationSource.Token, this);
            _room = new(_client, _lifetimeCancellationSource.Token);
        }

        MyNetLobbyServiceInterface MyNetInterface.Lobby => _lobby;
        MyNetPlayerServiceInterface MyNetInterface.Player { get; } = new AnonymousNetPlayerService();
        MyNetRoomServiceInterface MyNetInterface.Room => _room;

        internal async Task EnsureLaunchServerAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _server.StartAsync(cancellationToken);
            }
            catch (HttpListenerException)
            {
                if (await IsAvailableAsync(cancellationToken) == false)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        private async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using (var response = await _client.GetAsync(AnonymousServer.GetUri(AnonymousServer.ApiHealth), cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (response.StatusCode != HttpStatusCode.OK)
                        return false;

                    var content = await response.Content.ReadAsStringAsync();
                    cancellationToken.ThrowIfCancellationRequested();

                    return string.Equals(content, AnonymousServer.HealthResponse, StringComparison.Ordinal);
                }
            }
            catch (HttpRequestException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return false;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested == false)
            {
                return false;
            }
        }

        internal void Shutdown()
        {
            _lifetimeCancellationSource.Cancel();
            try
            {
                _server.Shutdown();
            }
            finally
            {
                _client.Dispose();
                _lifetimeCancellationSource.Dispose();
            }
        }
    }
}
