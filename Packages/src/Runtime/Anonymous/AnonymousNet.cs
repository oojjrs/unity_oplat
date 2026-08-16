using oojjrs.oplat.anonymous.controllers;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNet : MyNetInterface
    {
        private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(1) };
        private readonly CancellationTokenSource _lifetimeCancellationSource = new();
        private readonly CancellationToken _lifetimeCancellationToken;
        private readonly AnonymousNetLobbyService _lobby;
        private readonly AnonymousNetRoomService _room;
        private readonly AnonymousServer _server = new();

        internal AnonymousNet()
        {
            _lifetimeCancellationToken = _lifetimeCancellationSource.Token;
            _lobby = new(this);
            _room = new(this);
        }

        MyNetLobbyServiceInterface MyNetInterface.Lobby => _lobby;
        MyNetPlayerServiceInterface MyNetInterface.Player { get; } = new AnonymousNetPlayerService();
        MyNetRoomServiceInterface MyNetInterface.Room => _room;

        internal async Task AuthenticateAsync(string account, string nickname, CancellationToken callerCancellationToken)
        {
            using (var cancellationSource = CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                try
                {
                    await EnsureLaunchServerAsync(cancellationToken);

                    using (var response = await PostJsonAsync(AnonymousServer.ApiAuthenticate, new AnonymousServerAuthenticate.RequestArgument()
                    {
                        Account = account,
                        Nickname = nickname,
                    }, cancellationToken))
                    {
                        response.EnsureSuccessStatusCode();

                        var responseContent = await response.Content.ReadAsStringAsync();
                        var responseArgument = JsonUtility.FromJson<AnonymousServerAuthenticate.ResponseArgument>(responseContent);
                        if ((responseArgument == null) || string.IsNullOrEmpty(responseArgument.Token))
                            throw new FormatException("Invalid anonymous authentication response.");

                        cancellationToken.ThrowIfCancellationRequested();
                        _client.DefaultRequestHeaders.Remove(AnonymousServerAuthenticate.SessionHeader);
                        _client.DefaultRequestHeaders.Add(AnonymousServerAuthenticate.SessionHeader, responseArgument.Token);
                    }
                }
                catch (Exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }
            }
        }

        internal CancellationTokenSource CreateCancellationSource(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _lifetimeCancellationToken.ThrowIfCancellationRequested();
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCancellationToken);
        }

        private async Task EnsureLaunchServerAsync(CancellationToken cancellationToken)
        {
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

        internal Task<HttpResponseMessage> GetAsync(string api, CancellationToken cancellationToken)
        {
            return _client.GetAsync(AnonymousServer.GetUri(api), cancellationToken);
        }

        private async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            try
            {
                using (var response = await GetAsync(AnonymousServer.ApiHealth, cancellationToken))
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

        internal async Task<HttpResponseMessage> PostJsonAsync(string api, object argument, CancellationToken cancellationToken)
        {
            var content = JsonUtility.ToJson(argument);
            using (var requestContent = new StringContent(content, Encoding.UTF8, "application/json"))
                return await _client.PostAsync(AnonymousServer.GetUri(api), requestContent, cancellationToken);
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
