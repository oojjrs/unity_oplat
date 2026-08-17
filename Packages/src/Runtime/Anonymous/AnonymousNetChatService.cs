using oojjrs.oplat.anonymous.controllers;
using System;
using System.Text;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousNetChatService : MyNetChatServiceInterface
    {
        internal const int MessageByteCountMax = 4096;

        private readonly AnonymousNet Net;

        int MyNetChatServiceInterface.MessageByteCountMax => MessageByteCountMax;

        internal AnonymousNetChatService(AnonymousNet net)
        {
            Net = net;
        }

        async Task MyNetChatServiceInterface.ExitAsync(MyNetChatServiceInterface.ExitConfigInterface config, MyNetChatServiceInterface.ExitResultInterface result)
        {
            using (var cancellationSource = Net.CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                var roomId = config.RoomId;
                if (string.IsNullOrWhiteSpace(roomId))
                {
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                    return;
                }

                if (Net.UseLocal)
                {
                    result.OnOk(roomId);
                    return;
                }

                MyNetInterface.CatchInterface.FailureEnum? failure = null;
                try
                {
                    await Net.SendAsync(AnonymousNet.OperationEnum.ExitChat, new AnonymousServerExitChat.RequestArgument()
                    {
                        RoomId = roomId,
                    }, cancellationToken);
                    var response = await Net.ReceiveAsync(AnonymousNet.OperationEnum.ExitChat, cancellationToken);
                    if (response.ResultCode == AnonymousServerResponse.ResultCodeEnum.NotFound)
                        failure = MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom;
                    else
                        response.EnsureSuccess();
                }
                catch (Exception exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.OnException(new MyNetSessionException("Failed to exit anonymous chat.", exception));
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (failure.HasValue)
                {
                    result.OnFailed(failure.Value);
                    return;
                }

                result.OnOk(roomId);
            }
        }

        async Task MyNetChatServiceInterface.JoinAsync(MyNetChatServiceInterface.JoinConfigInterface config, MyNetChatServiceInterface.JoinResultInterface result)
        {
            using (var cancellationSource = Net.CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                var roomId = config.RoomId;
                if (string.IsNullOrWhiteSpace(roomId))
                {
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                    return;
                }

                if (Net.UseLocal)
                {
                    result.OnOk(roomId);
                    return;
                }

                MyNetInterface.CatchInterface.FailureEnum? failure = null;
                try
                {
                    await Net.SendAsync(AnonymousNet.OperationEnum.JoinChat, new AnonymousServerJoinChat.RequestArgument()
                    {
                        RoomId = roomId,
                    }, cancellationToken);
                    var response = await Net.ReceiveAsync(AnonymousNet.OperationEnum.JoinChat, cancellationToken);
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
                            break;
                    }
                }
                catch (Exception exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.OnException(new MyNetSessionException("Failed to join anonymous chat.", exception));
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (failure.HasValue)
                {
                    result.OnFailed(failure.Value);
                    return;
                }

                result.OnOk(roomId);
            }
        }

        async Task MyNetChatServiceInterface.SendAsync(MyNetChatServiceInterface.SendConfigInterface config, MyNetChatServiceInterface.SendResultInterface result)
        {
            using (var cancellationSource = Net.CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                var message = config.Message;
                var roomId = config.RoomId;
                if (string.IsNullOrWhiteSpace(roomId))
                {
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                    return;
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyMessage);
                    return;
                }

                if (Encoding.UTF8.GetByteCount(message) > MessageByteCountMax)
                {
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.MessageTooLong);
                    return;
                }

                if (Net.UseLocal)
                {
                    Net.ChatResult.OnReceived(message, Net.Account, roomId);
                    result.OnOk(roomId);
                    return;
                }

                MyNetInterface.CatchInterface.FailureEnum? failure = null;
                try
                {
                    await Net.SendAsync(AnonymousNet.OperationEnum.SendChat, new AnonymousServerSendChat.RequestArgument()
                    {
                        Message = message,
                        RoomId = roomId,
                    }, cancellationToken);
                    var response = await Net.ReceiveAsync(AnonymousNet.OperationEnum.SendChat, cancellationToken);
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
                            break;
                    }
                }
                catch (Exception exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.OnException(new MyNetSessionException("Failed to send anonymous chat message.", exception));
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (failure.HasValue)
                {
                    result.OnFailed(failure.Value);
                    return;
                }

                result.OnOk(roomId);
            }
        }
    }
}
