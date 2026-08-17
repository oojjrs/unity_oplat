using oojjrs.oplat.anonymous.controllers;
using System;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNetRoomService : MyNetRoomServiceInterface
    {
        private readonly AnonymousNet Net;

        internal AnonymousNetRoomService(AnonymousNet net)
        {
            Net = net;
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
                        IsPrivate = config.IsPrivate,
                        MaxPlayers = config.MaxPlayers,
                        Password = config.Password,
                        PlayerFields = AnonymousServerRoom.FieldData.FromNetFields(config.PlayerFields),
                        PlayerNickname = config.PlayerNickname,
                        RoomFields = AnonymousServerRoom.FieldData.FromNetFields(config.RoomFields),
                        Title = config.Title,
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
                        IsPrivate = config.IsPrivate,
                        RoomFields = AnonymousServerRoom.FieldData.FromNetFields(config.RoomFields),
                        RoomId = roomId,
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
