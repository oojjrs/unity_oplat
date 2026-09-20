using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat
{
    internal sealed class MyNetRoomSwitcher
    {
        private sealed class ExitConfig : MyNetRoomServiceInterface.ExitConfigInterface
        {
            public CancellationToken CancellationToken { get; }
            public string PlayerId { get; }
            public string RoomId { get; }

            internal ExitConfig(CancellationToken cancellationToken, string playerId, string roomId)
            {
                CancellationToken = cancellationToken;
                PlayerId = playerId;
                RoomId = roomId;
            }
        }

        private sealed class OperationResult : MyNetRoomServiceInterface.ExitResultInterface, MyNetRoomServiceInterface.JoinResultInterface
        {
            private enum StateEnum
            {
                None,
                Busy,
                Exception,
                Failed,
                Ok,
            }

            private MyNetSessionException _exception;
            private MyNetInterface.CatchInterface.FailureEnum _failure;
            private MyNetRoomInterface _room;
            private StateEnum _state;

            void MyNetInterface.CatchInterface.OnBusy()
            {
                _state = StateEnum.Busy;
            }

            void MyNetInterface.CatchInterface.OnException(MyNetSessionException e)
            {
                _exception = e;
                _state = StateEnum.Exception;
            }

            void MyNetInterface.CatchInterface.OnFailed(MyNetInterface.CatchInterface.FailureEnum e)
            {
                _failure = e;
                _state = StateEnum.Failed;
            }

            void MyNetRoomServiceInterface.ExitResultInterface.OnOk(string roomId, string playerId)
            {
                _state = StateEnum.Ok;
            }

            void MyNetRoomServiceInterface.JoinResultInterface.OnOk(MyNetRoomInterface room)
            {
                _room = room;
                _state = StateEnum.Ok;
            }

            internal void ForwardTo(MyNetRoomServiceInterface.JoinResultInterface result)
            {
                switch (_state)
                {
                    case StateEnum.Busy:
                        result.OnBusy();
                        return;
                    case StateEnum.Exception:
                        result.OnException(_exception);
                        return;
                    case StateEnum.Failed:
                        result.OnFailed(_failure);
                        return;
                    case StateEnum.Ok:
                        result.OnOk(_room);
                        return;
                    default:
                        result.OnException(new MyNetSessionException("The room operation completed without a result.", new InvalidOperationException()));
                        return;
                }
            }

            internal static OperationResult FromException(string message, Exception exception)
            {
                return new OperationResult
                {
                    _exception = new MyNetSessionException(message, exception),
                    _state = StateEnum.Exception,
                };
            }

            internal static OperationResult FromFailure(MyNetInterface.CatchInterface.FailureEnum failure)
            {
                return new OperationResult
                {
                    _failure = failure,
                    _state = StateEnum.Failed,
                };
            }

            internal static OperationResult FromOk(MyNetRoomInterface room)
            {
                return new OperationResult
                {
                    _room = room,
                    _state = StateEnum.Ok,
                };
            }

            internal bool IsOk => _state == StateEnum.Ok;
        }

        private sealed class RequestJoinConfig : MyNetRoomServiceInterface.JoinConfigInterface
        {
            public CancellationToken CancellationToken { get; }
            public string Code => null;
            public string Password { get; }
            public IEnumerable<MyNetInterface.Field> PlayerFields { get; }
            public string PlayerNickname { get; }
            public string RoomId { get; }

            internal RequestJoinConfig(CancellationToken cancellationToken, string roomId, MyNetRoomSwitchHandlerInterface.PreparationInterface preparation)
            {
                CancellationToken = cancellationToken;
                Password = preparation.Password;
                PlayerFields = preparation.PlayerFields;
                PlayerNickname = preparation.PlayerNickname;
                RoomId = roomId;
            }
        }

        private readonly Func<string> _getAccount;
        private readonly Func<CancellationToken, Task<MyNetRoomInterface>> _getCurrentRoomAsync;
        private readonly Func<MyNetRoomServiceInterface.ExitConfigInterface, MyNetRoomServiceInterface.ExitResultInterface, Task> _exitAsync;
        private readonly Func<MyNetRoomServiceInterface.JoinConfigInterface, MyNetRoomServiceInterface.JoinResultInterface, Task> _joinAsync;
        private readonly SemaphoreSlim _operationGate = new(1, 1);

        internal MyNetRoomSwitcher(Func<string> getAccount, Func<CancellationToken, Task<MyNetRoomInterface>> getCurrentRoomAsync, Func<MyNetRoomServiceInterface.ExitConfigInterface, MyNetRoomServiceInterface.ExitResultInterface, Task> exitAsync, Func<MyNetRoomServiceInterface.JoinConfigInterface, MyNetRoomServiceInterface.JoinResultInterface, Task> joinAsync)
        {
            _getAccount = getAccount;
            _getCurrentRoomAsync = getCurrentRoomAsync;
            _exitAsync = exitAsync;
            _joinAsync = joinAsync;
        }

        internal async Task HandleRequestAsync(string playerId, string roomId, MyNetRoomSwitchHandlerInterface handler, CancellationToken cancellationToken)
        {
            if (await _operationGate.WaitAsync(0, cancellationToken) == false)
            {
                handler.OnBusy();
                return;
            }

            OperationResult operationResult;
            try
            {
                var preparation = await handler.PrepareAsync(playerId, roomId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (preparation == null)
                    operationResult = OperationResult.FromException("Room switch preparation returned no configuration.", new InvalidOperationException());
                else
                    operationResult = await SwitchCoreAsync(new RequestJoinConfig(cancellationToken, roomId, preparation));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                operationResult = OperationResult.FromException("Failed to prepare room switch.", exception);
            }
            finally
            {
                _operationGate.Release();
            }

            operationResult.ForwardTo(handler);
        }

        internal async Task SwitchAsync(MyNetRoomServiceInterface.JoinConfigInterface config, MyNetRoomServiceInterface.JoinResultInterface result)
        {
            var cancellationToken = config.CancellationToken;
            if (await _operationGate.WaitAsync(0, cancellationToken) == false)
            {
                result.OnBusy();
                return;
            }

            OperationResult operationResult;
            try
            {
                operationResult = await SwitchCoreAsync(config);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                operationResult = OperationResult.FromException("Failed to switch room.", exception);
            }
            finally
            {
                _operationGate.Release();
            }

            operationResult.ForwardTo(result);
        }

        private async Task<OperationResult> SwitchCoreAsync(MyNetRoomServiceInterface.JoinConfigInterface config)
        {
            var cancellationToken = config.CancellationToken;
            if (string.IsNullOrWhiteSpace(config.RoomId) && string.IsNullOrWhiteSpace(config.Code))
                return OperationResult.FromFailure(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);

            var currentRoom = await _getCurrentRoomAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSameRoom(currentRoom, config))
                return OperationResult.FromOk(currentRoom);

            if (currentRoom != null)
            {
                var exitResult = new OperationResult();
                await _exitAsync(new ExitConfig(cancellationToken, _getAccount(), currentRoom.Id), exitResult);
                cancellationToken.ThrowIfCancellationRequested();
                if (exitResult.IsOk == false)
                    return exitResult;
            }

            var joinResult = new OperationResult();
            await _joinAsync(config, joinResult);
            cancellationToken.ThrowIfCancellationRequested();
            return joinResult;
        }

        private static bool IsSameRoom(MyNetRoomInterface room, MyNetRoomServiceInterface.JoinConfigInterface config)
        {
            if (room == null)
                return false;

            if (string.IsNullOrWhiteSpace(config.RoomId) == false)
                return room.Id == config.RoomId;

            return string.IsNullOrWhiteSpace(config.Code) == false && string.Equals(room.Code, config.Code, StringComparison.OrdinalIgnoreCase);
        }
    }
}
