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
            _lobby = new(_client, _lifetimeCancellationToken);
            _room = new(_client, _lifetimeCancellationToken);
        }

        MyNetLobbyServiceInterface MyNetInterface.Lobby => _lobby;
        MyNetPlayerServiceInterface MyNetInterface.Player { get; } = new AnonymousNetPlayerService();
        MyNetRoomServiceInterface MyNetInterface.Room => _room;

        internal async Task AuthenticateAsync(string account, string nickname, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _lifetimeCancellationToken.ThrowIfCancellationRequested();

                await EnsureLaunchServerAsync(cancellationToken);
                _lifetimeCancellationToken.ThrowIfCancellationRequested();

                var requestContent = JsonUtility.ToJson(new AnonymousServerAuthenticate.RequestArgument()
                {
                    Account = account,
                    Nickname = nickname,
                });
                using (var content = new StringContent(requestContent, Encoding.UTF8, "application/json"))
                {
                    using (var response = await _client.PostAsync(AnonymousServer.GetUri(AnonymousServer.ApiAuthenticate), content, cancellationToken))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        response.EnsureSuccessStatusCode();

                        var responseContent = await response.Content.ReadAsStringAsync();
                        cancellationToken.ThrowIfCancellationRequested();

                        var responseArgument = JsonUtility.FromJson<AnonymousServerAuthenticate.ResponseArgument>(responseContent);
                        if ((responseArgument == null) || string.IsNullOrEmpty(responseArgument.Token))
                            throw new FormatException("Invalid anonymous authentication response.");

                        cancellationToken.ThrowIfCancellationRequested();
                        _lifetimeCancellationToken.ThrowIfCancellationRequested();
                        _client.DefaultRequestHeaders.Remove(AnonymousServerAuthenticate.SessionHeader);
                        _client.DefaultRequestHeaders.Add(AnonymousServerAuthenticate.SessionHeader, responseArgument.Token);
                    }
                }
            }
            catch (Exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _lifetimeCancellationToken.ThrowIfCancellationRequested();
                throw;
            }
        }

        private async Task EnsureLaunchServerAsync(CancellationToken cancellationToken)
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
