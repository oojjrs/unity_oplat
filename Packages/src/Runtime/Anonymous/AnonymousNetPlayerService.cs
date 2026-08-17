using oojjrs.oplat.anonymous.controllers;
using System;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNetPlayerService : MyNetPlayerServiceInterface
    {
        private readonly AnonymousNet Net;

        internal AnonymousNetPlayerService(AnonymousNet net)
        {
            Net = net;
        }

        async Task MyNetPlayerServiceInterface.UpdateAsync(MyNetPlayerServiceInterface.UpdateConfigInterface config, MyNetPlayerServiceInterface.UpdateResultInterface result)
        {
            using (var cancellationSource = Net.CreateCancellationSource(config.CancellationToken))
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
                MyNetPlayerInterface player = null;
                try
                {
                    await Net.SendAsync(AnonymousNet.OperationEnum.UpdatePlayer, new AnonymousServerUpdatePlayer.RequestArgument()
                    {
                        PlayerFields = AnonymousServerRoom.FieldData.FromNetFields(config.PlayerFields),
                        PlayerId = playerId,
                        RoomId = roomId,
                    }, cancellationToken);
                    var response = await Net.ReceiveAsync(AnonymousNet.OperationEnum.UpdatePlayer, cancellationToken);
                    switch (response.ResultCode)
                    {
                        case AnonymousServerResponse.ResultCodeEnum.NotFound:
                            failure = MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom;
                            break;
                        case AnonymousServerResponse.ResultCodeEnum.Forbidden:
                            failure = MyNetInterface.CatchInterface.FailureEnum.NotPermitted;
                            break;
                        default:
                            response.EnsureSuccess();
                            player = (await response.GetContentAsync<AnonymousServerRoom.PlayerData>()).ToNetPlayer();
                            break;
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

                result.OnOk(player);
            }
        }
    }
}
