using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNetRoomService : MyNetRoomServiceInterface
    {
        private readonly HttpClient _client;
        private readonly CancellationToken _lifetimeCancellationToken;

        internal AnonymousNetRoomService(HttpClient client, CancellationToken lifetimeCancellationToken)
        {
            _client = client;
            _lifetimeCancellationToken = lifetimeCancellationToken;
        }

        async Task MyNetRoomServiceInterface.CreateAsync(MyNetRoomServiceInterface.CreateConfigInterface config, MyNetRoomServiceInterface.CreateResultInterface result)
        {
            var cancellationToken = config.CancellationToken;
            MyNetRoomInterface room;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _lifetimeCancellationToken.ThrowIfCancellationRequested();

                using (var content = new StringContent(new AnonymousNetRoomProtocol.RoomRequestArgument(config).ToJson(), Encoding.UTF8, "application/json"))
                {
                    using (var response = await _client.PostAsync(AnonymousServer.GetUri(AnonymousServer.ApiCreateRoom), content, cancellationToken))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        response.EnsureSuccessStatusCode();

                        var responseContent = await response.Content.ReadAsStringAsync();
                        cancellationToken.ThrowIfCancellationRequested();

                        room = AnonymousNetRoomProtocol.GetRoomFromJson(responseContent);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _lifetimeCancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _lifetimeCancellationToken.ThrowIfCancellationRequested();
                result.OnException(new MyNetSessionException("Failed to create anonymous room.", exception));
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            _lifetimeCancellationToken.ThrowIfCancellationRequested();
            result.OnOk(room);
        }

        async Task MyNetRoomServiceInterface.ExitAsync(MyNetRoomServiceInterface.ExitConfigInterface config, MyNetRoomServiceInterface.ExitResultInterface result)
        {
            var cancellationToken = config.CancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            _lifetimeCancellationToken.ThrowIfCancellationRequested();

            var playerId = config.PlayerId;
            var roomId = config.RoomId;
            if (string.IsNullOrWhiteSpace(playerId))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyPlayerId);
                return;
            }

            if (string.IsNullOrWhiteSpace(roomId))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                return;
            }

            MyNetInterface.CatchInterface.FailureEnum? failure = null;
            try
            {
                using (var content = new StringContent(new AnonymousNetRoomProtocol.ExitRequestArgument(playerId, roomId).ToJson(), Encoding.UTF8, "application/json"))
                {
                    using (var response = await _client.PostAsync(AnonymousServer.GetUri(AnonymousServer.ApiExitRoom), content, cancellationToken))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        switch (response.StatusCode)
                        {
                            case HttpStatusCode.NotFound:
                                failure = MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom;
                                break;
                            case HttpStatusCode.Forbidden:
                                failure = MyNetInterface.CatchInterface.FailureEnum.NotPermitted;
                                break;
                            default:
                                response.EnsureSuccessStatusCode();
                                break;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _lifetimeCancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _lifetimeCancellationToken.ThrowIfCancellationRequested();
                result.OnException(new MyNetSessionException("Failed to exit anonymous room.", exception));
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            _lifetimeCancellationToken.ThrowIfCancellationRequested();
            if (failure.HasValue)
            {
                result.OnFailed(failure.Value);
                return;
            }

            result.OnOk(roomId, playerId);
        }

        Task MyNetRoomServiceInterface.JoinAsync(MyNetRoomServiceInterface.JoinConfigInterface config, MyNetRoomServiceInterface.JoinResultInterface result)
        {
            throw new NotImplementedException();
        }

        Task MyNetRoomServiceInterface.UpdateAsync(MyNetRoomServiceInterface.UpdateConfigInterface config, MyNetRoomServiceInterface.UpdateResultInterface result)
        {
            throw new NotImplementedException();
        }
    }
}
