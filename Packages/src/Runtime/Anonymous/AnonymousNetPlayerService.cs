using oojjrs.oplat.anonymous.controllers;
using System;
using System.Net;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNetPlayerService : MyNetPlayerServiceInterface
    {
        private readonly AnonymousNet _net;

        internal AnonymousNetPlayerService(AnonymousNet net)
        {
            _net = net;
        }

        async Task MyNetPlayerServiceInterface.UpdateAsync(MyNetPlayerServiceInterface.UpdateConfigInterface config, MyNetPlayerServiceInterface.UpdateResultInterface result)
        {
            using (var cancellationSource = _net.CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
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
                    using (var response = await _net.PostJsonAsync(AnonymousServer.ApiUpdatePlayer, new AnonymousServerUpdatePlayer.RequestArgument()
                    {
                        PlayerFields = AnonymousNetRoomService.ConvertFields(config.PlayerFields),
                        PlayerId = playerId,
                        RoomId = roomId,
                    }, cancellationToken))
                    {
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
                catch (Exception exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.OnException(new MyNetSessionException("Failed to update anonymous player.", exception));
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (failure.HasValue)
                {
                    result.OnFailed(failure.Value);
                    return;
                }

                result.OnOk();
            }
        }
    }
}
