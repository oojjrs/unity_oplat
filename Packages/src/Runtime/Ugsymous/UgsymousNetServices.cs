using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using Unity.Services.Vivox;

namespace oojjrs.oplat.ugsymous
{
    internal sealed class UgsymousNetChatService : MyNetChatServiceInterface, IDisposable
    {
        private const int MessageByteCountMaxValue = 320;
        private readonly Dictionary<string, string> _channels = new();
        private readonly UgsymousNet _net;
        int MyNetChatServiceInterface.MessageByteCountMax => MessageByteCountMaxValue;

        internal UgsymousNetChatService(UgsymousNet net)
        {
            _net = net;
            VivoxService.Instance.ChannelMessageReceived += OnChannelMessageReceived;
        }

        async Task MyNetChatServiceInterface.ExitAsync(MyNetChatServiceInterface.ExitConfigInterface config, MyNetChatServiceInterface.ExitResultInterface result)
        {
            if (string.IsNullOrWhiteSpace(config.RoomId))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                return;
            }

            config.CancellationToken.ThrowIfCancellationRequested();
            var channel = ToChannel(config.RoomId);
            await VivoxService.Instance.LeaveChannelAsync(channel);
            _channels.Remove(channel);
            config.CancellationToken.ThrowIfCancellationRequested();
            result.OnOk(config.RoomId);
        }

        async Task MyNetChatServiceInterface.JoinAsync(MyNetChatServiceInterface.JoinConfigInterface config, MyNetChatServiceInterface.JoinResultInterface result)
        {
            if (string.IsNullOrWhiteSpace(config.RoomId))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                return;
            }

            config.CancellationToken.ThrowIfCancellationRequested();
            var channel = ToChannel(config.RoomId);
            await VivoxService.Instance.JoinGroupChannelAsync(channel, ChatCapability.TextOnly);
            _channels[channel] = config.RoomId;
            config.CancellationToken.ThrowIfCancellationRequested();
            result.OnOk(config.RoomId);
        }

        async Task MyNetChatServiceInterface.SendAsync(MyNetChatServiceInterface.SendConfigInterface config, MyNetChatServiceInterface.SendResultInterface result)
        {
            if (string.IsNullOrWhiteSpace(config.RoomId))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                return;
            }

            if (string.IsNullOrWhiteSpace(config.Message))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyMessage);
                return;
            }

            if (Encoding.UTF8.GetByteCount(config.Message) > MessageByteCountMaxValue)
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.MessageTooLong);
                return;
            }

            config.CancellationToken.ThrowIfCancellationRequested();
            await VivoxService.Instance.SendChannelTextMessageAsync(ToChannel(config.RoomId), config.Message);
            config.CancellationToken.ThrowIfCancellationRequested();
            result.OnOk(config.RoomId);
        }

        private void OnChannelMessageReceived(VivoxMessage message)
        {
            if (_channels.TryGetValue(message.ChannelName, out var roomId))
                _net.ChatResult.OnReceived(message.MessageText, message.SenderPlayerId, roomId);
        }

        private static string ToChannel(string roomId)
        {
            using var sha256 = SHA256.Create();
            return "oplat-" + UgsymousPlatform.ToHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(roomId)));
        }

        public void Dispose() => VivoxService.Instance.ChannelMessageReceived -= OnChannelMessageReceived;
    }

    internal sealed class UgsymousNetLobbyService : MyNetLobbyServiceInterface
    {
        private readonly Dictionary<string, UgsymousRoomStub> _rooms = new();
        private CancellationTokenSource _stopSource;
        private bool _isBusy;
        internal UgsymousNetLobbyService(UgsymousNet net) { }

        async Task MyNetLobbyServiceInterface.RefreshAsync(MyNetLobbyServiceInterface.ResultInterface result) => await RefreshAsync(result, CancellationToken.None);

        async Task MyNetLobbyServiceInterface.StartAsync(MyNetLobbyServiceInterface.ConfigInterface config, MyNetLobbyServiceInterface.ResultInterface result)
        {
            if (_isBusy)
            {
                result.OnBusy();
                return;
            }

            _isBusy = true;
            _stopSource = CancellationTokenSource.CreateLinkedTokenSource(config.CancellationToken);
            try
            {
                while (_stopSource.IsCancellationRequested == false)
                {
                    await RefreshAsync(result, _stopSource.Token);
                    await Task.Delay(TimeSpan.FromSeconds(config.PollingDelaySeconds), _stopSource.Token);
                }
            }
            catch (OperationCanceledException) when (_stopSource.IsCancellationRequested) { }
            finally
            {
                _isBusy = false;
                _stopSource.Dispose();
                _stopSource = null;
            }
        }

        public void Stop() => _stopSource?.Cancel();

        private async Task RefreshAsync(MyNetLobbyServiceInterface.ResultInterface result, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await MultiplayerService.Instance.QuerySessionsAsync(new());
                cancellationToken.ThrowIfCancellationRequested();
                result.OnOk(response.Sessions.Select(GetRoom).ToArray());
            }
            catch (SessionException e)
            {
                result.OnException(new(e.Message, e));
            }
        }

        private MyNetRoomInterface GetRoom(ISessionInfo session)
        {
            if (_rooms.TryGetValue(session.Id, out var room))
                room.Session = session;
            else
                _rooms.Add(session.Id, room = new(session));

            return room;
        }
    }

    internal sealed class UgsymousNetRoomService : MyNetRoomServiceInterface
    {
        private readonly Dictionary<string, UgsymousRoom> _rooms = new();
        private readonly UgsymousNet _net;
        private bool _isBusy;
        internal UgsymousNetRoomService(UgsymousNet net) => _net = net;

        async Task MyNetRoomServiceInterface.CreateAsync(MyNetRoomServiceInterface.CreateConfigInterface config, MyNetRoomServiceInterface.CreateResultInterface result)
        {
            await RunAsync(async () =>
            {
                var options = new SessionOptions { IsLocked = config.IsLocked, IsPrivate = config.IsPrivate, MaxPlayers = config.MaxPlayers, Name = config.Title, Password = config.Password, PlayerProperties = UgsymousNet.ToPlayerProperties(config.PlayerFields, config.PlayerNickname), SessionProperties = UgsymousNet.ToSessionProperties(config.RoomFields) };
                options.WithRelayNetwork().WithNetworkHandler(_net.Transport);
                var session = await MultiplayerService.Instance.CreateSessionAsync(options);
                config.CancellationToken.ThrowIfCancellationRequested();
                result.OnOk(GetRoom(session));
            }, result);
        }

        async Task MyNetRoomServiceInterface.JoinAsync(MyNetRoomServiceInterface.JoinConfigInterface config, MyNetRoomServiceInterface.JoinResultInterface result)
        {
            if (string.IsNullOrWhiteSpace(config.Code) && string.IsNullOrWhiteSpace(config.RoomId))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                return;
            }

            await RunAsync(async () =>
            {
                var options = new JoinSessionOptions { Password = config.Password, PlayerProperties = UgsymousNet.ToPlayerProperties(config.PlayerFields, config.PlayerNickname) };
                options.WithNetworkHandler(_net.Transport);
                var session = string.IsNullOrWhiteSpace(config.Code) ? await MultiplayerService.Instance.JoinSessionByIdAsync(config.RoomId, options) : await MultiplayerService.Instance.JoinSessionByCodeAsync(config.Code, options);
                config.CancellationToken.ThrowIfCancellationRequested();
                result.OnOk(GetRoom(session));
            }, result);
        }

        async Task MyNetRoomServiceInterface.ExitAsync(MyNetRoomServiceInterface.ExitConfigInterface config, MyNetRoomServiceInterface.ExitResultInterface result)
        {
            if (string.IsNullOrWhiteSpace(config.RoomId))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                return;
            }

            await RunAsync(async () =>
            {
                if (MultiplayerService.Instance.Sessions.TryGetValue(config.RoomId, out var session) == false)
                {
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);
                    return;
                }

                if (config.PlayerId == session.CurrentPlayer.Id)
                    await session.LeaveAsync();
                else if (session.Host == _net.Account)
                    await session.AsHost().RemovePlayerAsync(config.PlayerId);
                else
                {
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.NotPermitted);
                    return;
                }

                config.CancellationToken.ThrowIfCancellationRequested();
                _rooms.Remove(config.RoomId);
                result.OnOk(config.RoomId, config.PlayerId);
            }, result);
        }

        async Task MyNetRoomServiceInterface.UpdateAsync(MyNetRoomServiceInterface.UpdateConfigInterface config, MyNetRoomServiceInterface.UpdateResultInterface result)
        {
            await RunAsync(async () =>
            {
                if (MultiplayerService.Instance.Sessions.TryGetValue(config.RoomId, out var session) == false)
                {
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);
                    return;
                }

                session.AsHost().IsPrivate = config.IsPrivate;
                session.AsHost().SetProperties(UgsymousNet.ToSessionProperties(config.RoomFields));
                await session.AsHost().SavePropertiesAsync();
                config.CancellationToken.ThrowIfCancellationRequested();
                result.OnOk(GetRoom(session));
            }, result);
        }

        private MyNetRoomInterface GetRoom(ISession session)
        {
            if (_rooms.TryGetValue(session.Id, out var room))
                room.Session = session;
            else
                _rooms.Add(session.Id, room = new(session));

            return room;
        }

        private async Task RunAsync(Func<Task> action, MyNetInterface.CatchInterface result)
        {
            if (_isBusy)
            {
                result.OnBusy();
                return;
            }

            _isBusy = true;
            try { await action(); }
            catch (SessionException e) { result.OnException(new(e.Message, e)); }
            finally { _isBusy = false; }
        }
    }

    internal sealed class UgsymousNetPlayerService : MyNetPlayerServiceInterface
    {
        private readonly UgsymousNet _net;
        private bool _isBusy;
        internal UgsymousNetPlayerService(UgsymousNet net) => _net = net;

        async Task MyNetPlayerServiceInterface.UpdateAsync(MyNetPlayerServiceInterface.UpdateConfigInterface config, MyNetPlayerServiceInterface.UpdateResultInterface result)
        {
            if (_isBusy) { result.OnBusy(); return; }
            if (string.IsNullOrWhiteSpace(config.PlayerId)) { result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyPlayerId); return; }
            _isBusy = true;
            try
            {
                if (MultiplayerService.Instance.Sessions.TryGetValue(config.RoomId, out var session) == false) { result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom); return; }
                if (session.CurrentPlayer.Id != config.PlayerId) { result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.NotPermitted); return; }
                session.CurrentPlayer.SetProperties(UgsymousNet.ToPlayerProperties(config.PlayerFields));
                await session.SaveCurrentPlayerDataAsync();
                config.CancellationToken.ThrowIfCancellationRequested();
                result.OnOk(new UgsymousPlayer(new UgsymousRoom(session), session.CurrentPlayer));
                _net.PlayerResult.OnOk(new UgsymousPlayer(new UgsymousRoom(session), session.CurrentPlayer));
            }
            catch (SessionException e) { result.OnException(new(e.Message, e)); }
            finally { _isBusy = false; }
        }
    }
}
