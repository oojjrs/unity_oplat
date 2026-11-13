using oojjrs.oplat.anonymous.controllers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNetRoomService : MyNetRoomServiceInterface
    {
        private readonly SemaphoreSlim _joinInfoGate = new(1, 1);
        private readonly AnonymousNet Net;
        private readonly MyNetRoomSwitcher Switcher;

        internal AnonymousNetRoomService(AnonymousNet net)
        {
            Net = net;
            Switcher = new(() => Net.Account, Net.GetCurrentRoomAsync, (config, result) => ((MyNetRoomServiceInterface)this).ExitAsync(config, result), (config, result) => ((MyNetRoomServiceInterface)this).JoinAsync(config, result));
        }

        async Task MyNetRoomServiceInterface.CreateAsync(MyNetRoomServiceInterface.CreateConfigInterface config, MyNetRoomServiceInterface.CreateResultInterface result)
        {
            using (var cancellationSource = Net.CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                MyNetRoomInterface room;
                try
                {
                    await Net.SendAsync(AnonymousNet.OperationEnum.CreateRoom, new AnonymousServerCreateRoom.RequestArgument()
                    {
                        IsLocked = config.IsLocked,
                        MaxPlayers = config.MaxPlayers,
                        Password = config.Password,
                        PlayerFields = AnonymousServerRoom.FieldData.FromNetFields(config.PlayerFields),
                        PlayerNickname = config.PlayerNickname,
                        RoomFields = AnonymousServerRoom.FieldData.FromNetFields(config.RoomFields),
                        Title = config.Title,
                        Visibility = config.Visibility,
                    }, cancellationToken);
                    var response = await Net.ReceiveAsync(AnonymousNet.OperationEnum.CreateRoom, cancellationToken);
                    response.EnsureSuccess();
                    room = (await response.GetContentAsync<AnonymousServerRoom.RoomData>()).ToNetRoom();
                }
                catch (Exception exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.OnException(new MyNetSessionException("Failed to create anonymous room.", exception));
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                Net.SetCurrentRoom(room);
                result.OnOk(room);
            }
        }

        async Task MyNetRoomServiceInterface.ExitAsync(MyNetRoomServiceInterface.ExitConfigInterface config, MyNetRoomServiceInterface.ExitResultInterface result)
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
                try
                {
                    await Net.SendAsync(AnonymousNet.OperationEnum.ExitRoom, new AnonymousServerExitRoom.RequestArgument()
                    {
                        PlayerId = playerId,
                        RoomId = roomId,
                    }, cancellationToken);
                    var response = await Net.ReceiveAsync(AnonymousNet.OperationEnum.ExitRoom, cancellationToken);
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
                    result.OnException(new MyNetSessionException("Failed to exit anonymous room.", exception));
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (failure.HasValue)
                {
                    result.OnFailed(failure.Value);
                    return;
                }

                Net.ClearCurrentRoomForPlayer(roomId, playerId);
                result.OnOk(roomId, playerId);
            }
        }

        async Task MyNetRoomServiceInterface.GetJoinInfoAsync(MyNetRoomServiceInterface.JoinInfoConfigInterface config, MyNetRoomServiceInterface.JoinInfoResultInterface result)
        {
            using (var cancellationSource = Net.CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                if (string.IsNullOrWhiteSpace(config.RoomId))
                {
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                    return;
                }

                if (await _joinInfoGate.WaitAsync(0, cancellationToken) == false)
                {
                    result.OnBusy();
                    return;
                }

                MyNetRoomServiceInterface.JoinInfo info = default;
                MyNetInterface.CatchInterface.FailureEnum? failure = null;
                Exception caughtException = null;
                try
                {
                    using (var requestSource = Net.CreateCancellationSource(CancellationToken.None))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Net.SendAsync(AnonymousNet.OperationEnum.GetRoomJoinInfo, new AnonymousServer.RoomJoinInfoRequestArgument() { RoomId = config.RoomId }, requestSource.Token);
                        var response = await Net.ReceiveAsync(AnonymousNet.OperationEnum.GetRoomJoinInfo, requestSource.Token);
                        if (response.ResultCode == AnonymousServerResponse.ResultCodeEnum.NotFound)
                        {
                            failure = MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom;
                        }
                        else
                        {
                            response.EnsureSuccess();
                            var argument = await response.GetContentAsync<AnonymousServer.RoomJoinInfoResponseArgument>();
                            if ((argument == null) || (argument.RoomId != config.RoomId.Trim()))
                                throw new FormatException("Invalid anonymous room join information response.");

                            info = new(argument.RoomId, argument.Title, argument.IsPasswordRequired);
                        }
                    }
                }
                catch (Exception exception)
                {
                    caughtException = exception;
                }
                finally
                {
                    _joinInfoGate.Release();
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (failure.HasValue)
                    result.OnFailed(failure.Value);
                else if (caughtException != null)
                    result.OnException(new MyNetSessionException("Failed to get anonymous room join information.", caughtException));
                else
                    result.OnOk(info);
            }
        }

        async Task MyNetRoomServiceInterface.JoinAsync(MyNetRoomServiceInterface.JoinConfigInterface config, MyNetRoomServiceInterface.JoinResultInterface result)
        {
            using (var cancellationSource = Net.CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                var code = config.Code;
                var roomId = config.RoomId;
                if (string.IsNullOrWhiteSpace(roomId) && string.IsNullOrWhiteSpace(code))
                {
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                    return;
                }

                MyNetInterface.CatchInterface.FailureEnum? failure = null;
                MyNetRoomInterface room = null;
                try
                {
                    await Net.SendAsync(AnonymousNet.OperationEnum.JoinRoom, new AnonymousServerJoinRoom.RequestArgument()
                    {
                        Code = code,
                        Password = config.Password,
                        PlayerFields = AnonymousServerRoom.FieldData.FromNetFields(config.PlayerFields),
                        PlayerNickname = config.PlayerNickname,
                        RoomId = roomId,
                    }, cancellationToken);
                    var response = await Net.ReceiveAsync(AnonymousNet.OperationEnum.JoinRoom, cancellationToken);
                    switch (response.ResultCode)
                    {
                        case AnonymousServerResponse.ResultCodeEnum.IncorrectPassword:
                            failure = MyNetInterface.CatchInterface.FailureEnum.IncorrectPassword;
                            break;
                        case AnonymousServerResponse.ResultCodeEnum.RoomFull:
                            failure = MyNetInterface.CatchInterface.FailureEnum.RoomFull;
                            break;
                        case AnonymousServerResponse.ResultCodeEnum.RoomLocked:
                            failure = MyNetInterface.CatchInterface.FailureEnum.RoomLocked;
                            break;
                        case AnonymousServerResponse.ResultCodeEnum.NotFound:
                            failure = MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom;
                            break;
                        case AnonymousServerResponse.ResultCodeEnum.Forbidden:
                        case AnonymousServerResponse.ResultCodeEnum.Conflict:
                            failure = MyNetInterface.CatchInterface.FailureEnum.NotPermitted;
                            break;
                        default:
                            response.EnsureSuccess();
                            room = (await response.GetContentAsync<AnonymousServerRoom.RoomData>()).ToNetRoom();
                            break;
                    }
                }
                catch (Exception exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.OnException(new MyNetSessionException("Failed to join anonymous room.", exception));
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (failure.HasValue)
                {
                    result.OnFailed(failure.Value);
                    return;
                }

                Net.SetCurrentRoom(room);
                result.OnOk(room);
            }
        }

        Task MyNetRoomServiceInterface.SwitchAsync(MyNetRoomServiceInterface.JoinConfigInterface config, MyNetRoomServiceInterface.JoinResultInterface result)
        {
            return Switcher.SwitchAsync(config, result);
        }

        async Task MyNetRoomServiceInterface.UpdateAsync(MyNetRoomServiceInterface.UpdateConfigInterface config, MyNetRoomServiceInterface.UpdateResultInterface result)
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

                MyNetInterface.CatchInterface.FailureEnum? failure = null;
                MyNetRoomInterface room = null;
                try
                {
                    await Net.SendAsync(AnonymousNet.OperationEnum.UpdateRoom, new AnonymousServerUpdateRoom.RequestArgument()
                    {
                        RoomFields = AnonymousServerRoom.FieldData.FromNetFields(config.RoomFields),
                        RoomId = roomId,
                        Visibility = config.Visibility,
                    }, cancellationToken);
                    var response = await Net.ReceiveAsync(AnonymousNet.OperationEnum.UpdateRoom, cancellationToken);
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
                            room = (await response.GetContentAsync<AnonymousServerRoom.RoomData>()).ToNetRoom();
                            break;
                    }
                }
                catch (Exception exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.OnException(new MyNetSessionException("Failed to update anonymous room.", exception));
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (failure.HasValue)
                {
                    result.OnFailed(failure.Value);
                    return;
                }

                result.OnOk(room);
            }
        }

    }
}
