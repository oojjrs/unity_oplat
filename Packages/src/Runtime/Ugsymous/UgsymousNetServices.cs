using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
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
            await VivoxService.Instance.SendChannelTextMessageAsync(ToChannel(config.RoomId), config.Message, new MessageOptions()
            {
                Metadata = _net.Time.UtcNow.ToString(),
            });
            config.CancellationToken.ThrowIfCancellationRequested();
            result.OnOk(config.RoomId);
        }

        private void OnChannelMessageReceived(VivoxMessage message)
        {
            if (_channels.TryGetValue(message.ChannelName, out var roomId))
            {
                var sentAt = MyTime.TryParse(message.Metadata, out var value) ? value : _net.Time.UtcNow;
                _net.ChatResult.OnReceived(message.MessageText, message.SenderPlayerId, roomId, sentAt);
            }
        }

        private static string ToChannel(string roomId)
        {
            using var sha256 = SHA256.Create();
            return "oplat-" + UgsymousPlatform.ToHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(roomId)));
        }

        public void Dispose() => VivoxService.Instance.ChannelMessageReceived -= OnChannelMessageReceived;
    }

    internal sealed class UgsymousNetFriendService : MyNetFriendServiceInterface, IDisposable
    {
        private const int MinimumPollingDelaySeconds = 1;

        private readonly SemaphoreSlim _addGate = new(1, 1);
        private readonly SemaphoreSlim _inviteGate = new(1, 1);
        private readonly UgsymousNet _net;
        private readonly SemaphoreSlim _refreshGate = new(1, 1);
        private readonly IFriendsService _service;

        private MyNetFriendServiceInterface.ConfigInterface _config;
        private float _nextUpdateTimeSeconds;
        private int _pollingGeneration;
        private MyNetFriendServiceInterface.ResultInterface _result;

        internal UgsymousNetFriendService(UgsymousNet net)
        {
            _net = net;
            _service = FriendsService.Instance;
            _service.MessageReceived += OnMessageReceived;
        }

        async Task MyNetFriendServiceInterface.InviteAsync(MyNetFriendServiceInterface.InviteConfigInterface config, MyNetFriendServiceInterface.InviteResultInterface result)
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

                if (playerId == _net.Account)
                {
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.NotPermitted);
                    return;
                }

                if (await _inviteGate.WaitAsync(0, cancellationToken) == false)
                {
                    result.OnBusy();
                    return;
                }

                MyNetInterface.CatchInterface.FailureEnum? failure = null;
                Exception caughtException = null;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (MultiplayerService.Instance.Sessions.TryGetValue(roomId, out var session) == false)
                        failure = MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom;
                    else if ((session.CurrentPlayer == null) || (session.CurrentPlayer.Id != _net.Account))
                        failure = MyNetInterface.CatchInterface.FailureEnum.NotPermitted;
                    else
                    {
                        var relationship = _service.Friends.FirstOrDefault(t => t.Member?.Id == playerId);
                        if ((relationship == null) || (IsAvailable(relationship.Member?.Presence?.Availability ?? Availability.Offline) == false))
                            failure = MyNetInterface.CatchInterface.FailureEnum.NotPermitted;
                        else
                            await _service.MessageAsync(playerId, new UgsymousFriendInvitation { Kind = UgsymousFriendInvitation.KindValue, RoomId = roomId });
                    }
                }
                catch (Exception exception)
                {
                    caughtException = exception;
                }
                finally
                {
                    _inviteGate.Release();
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (caughtException != null)
                    result.OnException(new MyNetSessionException("Failed to send UGS friend invitation.", caughtException));
                else if (failure.HasValue)
                    result.OnFailed(failure.Value);
                else
                    result.OnOk(roomId, playerId);
            }
        }

        Task MyNetFriendServiceInterface.RefreshAsync(MyNetFriendServiceInterface.ResultInterface result) => RefreshAsync(CancellationToken.None, result, null);

        async Task MyNetFriendServiceInterface.RequestAddAsync(MyNetFriendServiceInterface.RequestAddConfigInterface config, MyNetFriendServiceInterface.RequestAddResultInterface result)
        {
            using (var cancellationSource = _net.CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                var playerId = config.PlayerId;
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyPlayerId);
                    return;
                }

                if (playerId == _net.Account)
                {
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.NotPermitted);
                    return;
                }

                if (await _addGate.WaitAsync(0, cancellationToken) == false)
                {
                    result.OnBusy();
                    return;
                }

                Exception caughtException = null;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if ((_service.Friends.Any(t => t.Member?.Id == playerId) == false) && (_service.OutgoingFriendRequests.Any(t => t.Member?.Id == playerId) == false))
                        await _service.AddFriendAsync(playerId);
                }
                catch (Exception exception)
                {
                    caughtException = exception;
                }
                finally
                {
                    _addGate.Release();
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (caughtException == null)
                    result.OnOk(playerId);
                else
                    result.OnException(new MyNetSessionException("Failed to add UGS friend.", caughtException));
            }
        }

        Task MyNetFriendServiceInterface.StartAsync(MyNetFriendServiceInterface.ConfigInterface config, MyNetFriendServiceInterface.ResultInterface result)
        {
            config.CancellationToken.ThrowIfCancellationRequested();
            _net.LifetimeCancellationToken.ThrowIfCancellationRequested();
            ++_pollingGeneration;
            _config = config;
            _result = result;
            _nextUpdateTimeSeconds = float.PositiveInfinity;
            return RefreshPollingAsync(config, result, _pollingGeneration);
        }

        void MyNetFriendServiceInterface.Stop() => StopPolling();

        private async Task<MyNetFriendInterface[]> ReadFriendsAsync(CancellationToken cancellationToken)
        {
            var session = MultiplayerService.Instance.Sessions.Values.FirstOrDefault(t => t.CurrentPlayer?.Id == _net.Account);
            var roomId = (session != null) && (UgsymousNet.GetVisibility(session) != MyNetRoomInterface.VisibilityEnum.Private) ? session.Id : string.Empty;
            await _service.SetPresenceAsync(Availability.Online, new UgsymousFriendActivity { RoomId = roomId });
            cancellationToken.ThrowIfCancellationRequested();
            await _service.ForceRelationshipsRefreshAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return _service.Friends.Select(t => (MyNetFriendInterface)new UgsymousFriend(t)).ToArray();
        }

        private async Task RefreshAsync(CancellationToken callerCancellationToken, MyNetFriendServiceInterface.ResultInterface result, int? pollingGeneration)
        {
            using (var cancellationSource = _net.CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                if (pollingGeneration.HasValue)
                    await _refreshGate.WaitAsync(cancellationToken);
                else if (await _refreshGate.WaitAsync(0, cancellationToken) == false)
                {
                    result.OnBusy();
                    return;
                }

                MyNetFriendInterface[] friends = null;
                Exception caughtException = null;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (pollingGeneration.HasValue && (pollingGeneration.Value != _pollingGeneration))
                        return;

                    friends = await ReadFriendsAsync(cancellationToken);
                }
                catch (Exception exception)
                {
                    caughtException = exception;
                }
                finally
                {
                    _refreshGate.Release();
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (pollingGeneration.HasValue && (pollingGeneration.Value != _pollingGeneration))
                    return;

                if (caughtException == null)
                    result.OnOk(friends);
                else
                    result.OnException(new MyNetSessionException("Failed to get UGS friends.", caughtException));
            }
        }

        private async Task RefreshPollingAsync(MyNetFriendServiceInterface.ConfigInterface config, MyNetFriendServiceInterface.ResultInterface result, int pollingGeneration)
        {
            try
            {
                await RefreshAsync(config.CancellationToken, result, pollingGeneration);
            }
            finally
            {
                if (pollingGeneration == _pollingGeneration)
                {
                    if (config.CancellationToken.IsCancellationRequested || _net.LifetimeCancellationToken.IsCancellationRequested)
                        StopPolling();
                    else
                        _nextUpdateTimeSeconds = UnityEngine.Time.realtimeSinceStartup + Math.Max(MinimumPollingDelaySeconds, config.PollingDelaySeconds);
                }
            }
        }

        private void OnMessageReceived(IMessageReceivedEvent message)
        {
            UgsymousFriendInvitation invitation;
            try
            {
                invitation = message.GetAs<UgsymousFriendInvitation>();
            }
            catch
            {
                return;
            }

            if ((invitation?.Kind == UgsymousFriendInvitation.KindValue) && string.IsNullOrWhiteSpace(message.UserId) == false && string.IsNullOrWhiteSpace(invitation.RoomId) == false)
                _net.FriendResult.OnInvited(message.UserId, invitation.RoomId);
        }

        private static bool IsAvailable(Availability availability) => (availability == Availability.Online) || (availability == Availability.Busy) || (availability == Availability.Away);

        private void StopPolling()
        {
            ++_pollingGeneration;
            _config = null;
            _result = null;
        }

        internal async void Update()
        {
            var config = _config;
            if (config == null)
                return;

            if (config.CancellationToken.IsCancellationRequested || _net.LifetimeCancellationToken.IsCancellationRequested)
            {
                StopPolling();
                return;
            }

            if (UnityEngine.Time.realtimeSinceStartup < _nextUpdateTimeSeconds)
                return;

            var result = _result;
            var pollingGeneration = _pollingGeneration;
            _nextUpdateTimeSeconds = float.PositiveInfinity;
            try
            {
                await RefreshPollingAsync(config, result, pollingGeneration);
            }
            catch (OperationCanceledException) when (config.CancellationToken.IsCancellationRequested || _net.LifetimeCancellationToken.IsCancellationRequested)
            {
            }
        }

        public void Dispose()
        {
            StopPolling();
            _service.MessageReceived -= OnMessageReceived;
        }
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

        private static bool IsDisconnected(LobbyExceptionReason reason) => (reason == LobbyExceptionReason.NetworkError) || (reason == LobbyExceptionReason.BadGateway) || (reason == LobbyExceptionReason.ServiceUnavailable) || (reason == LobbyExceptionReason.GatewayTimeout);

        private async Task RefreshAsync(MyNetLobbyServiceInterface.ResultInterface result, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await LobbyService.Instance.QueryLobbiesAsync(new() { Count = 100 });
                cancellationToken.ThrowIfCancellationRequested();
                result.OnOk(response.Results.Select(GetRoom).ToArray());
            }
            catch (LobbyServiceException e)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsDisconnected(e.Reason))
                {
                    Stop();
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.Disconnected);
                }
                else
                {
                    result.OnException(new(e.Message, e));
                }
            }
            catch (Exception e)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.OnException(new(e.Message, e));
            }
        }

        private MyNetRoomInterface GetRoom(Lobby session)
        {
            if (_rooms.TryGetValue(session.Id, out var room))
                room.Session = session;
            else
                _rooms.Add(session.Id, room = new(session));

            return room;
        }
    }

    internal sealed class UgsymousNetRoomService : MyNetRoomServiceInterface, IDisposable
    {
        private readonly HashSet<string> _exitingRoomIds = new();
        private readonly Dictionary<string, (ISession session, Action<string> handler)> _hostChangeRegistrations = new();
        private readonly UgsymousNet _net;
        private readonly Dictionary<string, UgsymousRoom> _rooms = new();
        private readonly MyNetRoomSwitcher _switcher;
        private bool _isBusy;

        internal UgsymousNetRoomService(UgsymousNet net)
        {
            _net = net;
            _switcher = new(() => _net.Account, GetCurrentRoomAsync, (config, result) => ((MyNetRoomServiceInterface)this).ExitAsync(config, result), (config, result) => ((MyNetRoomServiceInterface)this).JoinAsync(config, result));
            MultiplayerService.Instance.SessionRemoved += OnSessionRemoved;
        }

        async Task MyNetRoomServiceInterface.CreateAsync(MyNetRoomServiceInterface.CreateConfigInterface config, MyNetRoomServiceInterface.CreateResultInterface result)
        {
            await RunAsync(async () =>
            {
                var options = new SessionOptions { IsLocked = config.IsLocked, IsPrivate = UgsymousNet.ToIsPrivate(config.Visibility), MaxPlayers = config.MaxPlayers, Name = config.Title, Password = config.Password, PlayerProperties = UgsymousNet.ToPlayerProperties(config.PlayerFields, config.PlayerNickname), SessionProperties = UgsymousNet.ToSessionProperties(config.RoomFields, config.Visibility) };
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

        Task MyNetRoomServiceInterface.SwitchAsync(MyNetRoomServiceInterface.JoinConfigInterface config, MyNetRoomServiceInterface.JoinResultInterface result)
        {
            return _switcher.SwitchAsync(config, result);
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
                {
                    _exitingRoomIds.Add(config.RoomId);
                    try
                    {
                        if (session.Host == _net.Account)
                            await session.AsHost().DeleteAsync();
                        else
                            await session.LeaveAsync();
                    }
                    finally
                    {
                        _exitingRoomIds.Remove(config.RoomId);
                    }
                }
                else if (session.Host == _net.Account)
                    await session.AsHost().RemovePlayerAsync(config.PlayerId);
                else
                {
                    result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.NotPermitted);
                    return;
                }

                config.CancellationToken.ThrowIfCancellationRequested();
                if (config.PlayerId == session.CurrentPlayer.Id)
                    RemoveRoom(config.RoomId);

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

                session.AsHost().IsPrivate = UgsymousNet.ToIsPrivate(config.Visibility);
                session.AsHost().SetProperties(UgsymousNet.ToSessionProperties(config.RoomFields, config.Visibility));
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
            {
                _rooms.Add(session.Id, room = new(session));
                Action<string> handler = _ => OnSessionHostChanged(session);
                _hostChangeRegistrations.Add(session.Id, (session, handler));
                session.SessionHostChanged += handler;
            }

            return room;
        }

        private Task<MyNetRoomInterface> GetCurrentRoomAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var session = MultiplayerService.Instance.Sessions.Values.FirstOrDefault(value => value.CurrentPlayer?.Id == _net.Account);
            return Task.FromResult(session == null ? null : GetRoom(session));
        }

        private async void OnSessionHostChanged(ISession session)
        {
            if ((_rooms.ContainsKey(session.Id) == false) || (_exitingRoomIds.Add(session.Id) == false))
                return;

            SessionException caughtException = null;
            try
            {
                if (session.Host == _net.Account)
                    await session.AsHost().DeleteAsync();
                else
                    await session.LeaveAsync();
            }
            catch (SessionException e)
            {
                caughtException = e;
            }
            finally
            {
                _exitingRoomIds.Remove(session.Id);
                RemoveRoom(session.Id);
            }

            if (_net.LifetimeCancellationToken.IsCancellationRequested)
                return;

            if (caughtException != null)
                _net.RoomResult.OnException(new("Failed to close UGS room after host migration.", caughtException));

            _net.RoomResult.OnFailed(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);
        }

        private void OnSessionRemoved(ISession session)
        {
            if (RemoveRoom(session.Id) == false)
                return;

            if (_exitingRoomIds.Remove(session.Id) == false)
                _net.RoomResult.OnFailed(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);
        }

        private bool RemoveRoom(string roomId)
        {
            if (_hostChangeRegistrations.Remove(roomId, out var registration))
                registration.session.SessionHostChanged -= registration.handler;

            return _rooms.Remove(roomId);
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

        public void Dispose()
        {
            MultiplayerService.Instance.SessionRemoved -= OnSessionRemoved;
            foreach (var registration in _hostChangeRegistrations.Values)
                registration.session.SessionHostChanged -= registration.handler;

            _hostChangeRegistrations.Clear();
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
