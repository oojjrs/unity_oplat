using oojjrs.oplat.anonymous.controllers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNet : MyNetInterface
    {
        internal enum OperationEnum : byte
        {
            AddFriend = 13,
            AddStats = 16,
            Authenticate = 1,
            CreateRoom = 2,
            EnsureStats = 15,
            ExitChat = 9,
            ExitRoom = 3,
            GetCurrentRoom = 8,
            GetFriends = 12,
            GetRooms = 4,
            InviteFriend = 14,
            JoinChat = 10,
            JoinRoom = 5,
            ResetStat = 18,
            ResetStats = 17,
            SendChat = 11,
            UpdateAverageRateStat = 19,
            UpdatePlayer = 6,
            UpdateRoom = 7,
        }

        private enum RoomRoleEnum : byte
        {
            Host = 1,
            Member = 2,
            None = 0,
        }

        private sealed class AnonymousNetFriend : MyNetFriendInterface
        {
            private readonly AnonymousServer.FriendData _data;

            public AnonymousNetFriend(AnonymousServer.FriendData data)
            {
                _data = data;
            }

            string MyNetFriendInterface.Id => _data.Id;
            string MyNetFriendInterface.Nickname => _data.Nickname;
            string MyNetFriendInterface.RoomId => _data.RoomId;
            MyNetFriendInterface.StateEnum MyNetFriendInterface.State => _data.State;
        }

        private sealed class AnonymousNetFriendService : MyNetFriendServiceInterface
        {
            private const int MinimumPollingDelaySeconds = 1;

            private readonly SemaphoreSlim _addGate = new(1, 1);
            private readonly SemaphoreSlim _inviteGate = new(1, 1);
            private readonly AnonymousNet _net;
            private readonly SemaphoreSlim _refreshGate = new(1, 1);

            private MyNetFriendServiceInterface.ConfigInterface _config;
            private float _nextUpdateTimeSeconds;
            private int _pollingGeneration;
            private MyNetFriendServiceInterface.ResultInterface _result;

            public AnonymousNetFriendService(AnonymousNet net)
            {
                _net = net;
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
                        await _net.SendAsync(OperationEnum.InviteFriend, new AnonymousServer.InviteFriendRequestArgument() { PlayerId = playerId, RoomId = roomId }, _net.LifetimeCancellationToken);
                        var response = await _net.ReceiveAsync(OperationEnum.InviteFriend, _net.LifetimeCancellationToken);
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
                        caughtException = exception;
                    }
                    finally
                    {
                        _inviteGate.Release();
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    if (caughtException != null)
                        result.OnException(new MyNetSessionException("Failed to send anonymous friend invitation.", caughtException));
                    else if (failure.HasValue)
                        result.OnFailed(failure.Value);
                    else
                        result.OnOk(roomId, playerId);
                }
            }

            Task MyNetFriendServiceInterface.RefreshAsync(MyNetFriendServiceInterface.ResultInterface result)
            {
                return RefreshAsync(CancellationToken.None, result, null);
            }

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

                    if (await _addGate.WaitAsync(0, cancellationToken) == false)
                    {
                        result.OnBusy();
                        return;
                    }

                    Exception caughtException = null;
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await _net.SendAsync(OperationEnum.AddFriend, new AnonymousServer.AddFriendRequestArgument() { PlayerId = playerId }, _net.LifetimeCancellationToken);
                        var response = await _net.ReceiveAsync(OperationEnum.AddFriend, _net.LifetimeCancellationToken);
                        response.EnsureSuccess();
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
                        result.OnException(new MyNetSessionException("Failed to add anonymous friend.", caughtException));
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

            void MyNetFriendServiceInterface.Stop()
            {
                StopPolling();
            }

            private async Task<MyNetFriendInterface[]> ReadFriendsAsync()
            {
                var cancellationToken = _net.LifetimeCancellationToken;
                await _net.SendAsync(OperationEnum.GetFriends, null, cancellationToken);
                var response = await _net.ReceiveAsync(OperationEnum.GetFriends, cancellationToken);
                response.EnsureSuccess();
                var responseArgument = await response.GetContentAsync<AnonymousServer.FriendsResponseArgument>();
                if ((responseArgument == null) || (responseArgument.Friends == null))
                    throw new FormatException("Invalid anonymous friends response.");

                var friends = new MyNetFriendInterface[responseArgument.Friends.Length];
                for (var index = 0; index < friends.Length; ++index)
                {
                    var data = responseArgument.Friends[index];
                    if ((data == null) || string.IsNullOrEmpty(data.Id) || (data.Nickname == null) || (data.RoomId == null) || ((data.State != MyNetFriendInterface.StateEnum.Online) && (data.State != MyNetFriendInterface.StateEnum.Offline)))
                        throw new FormatException("Invalid anonymous friend response.");

                    friends[index] = new AnonymousNetFriend(data);
                }

                return friends;
            }

            private async Task RefreshAsync(CancellationToken callerCancellationToken, MyNetFriendServiceInterface.ResultInterface result, int? pollingGeneration)
            {
                using (var cancellationSource = _net.CreateCancellationSource(callerCancellationToken))
                {
                    var cancellationToken = cancellationSource.Token;
                    if (pollingGeneration.HasValue)
                    {
                        await _refreshGate.WaitAsync(cancellationToken);
                    }
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

                        friends = await ReadFriendsAsync();
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
                        result.OnException(new MyNetSessionException("Failed to get anonymous friends.", caughtException));
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

            public void StopPolling()
            {
                ++_pollingGeneration;
                _config = null;
                _result = null;
            }

            public async void Update()
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
        }

        private sealed class AnonymousStatsService : MyStatsServiceInterface
        {
            private readonly SemaphoreSlim _gate = new(1, 1);
            private readonly AnonymousNet _net;

            public AnonymousStatsService(AnonymousNet net)
            {
                _net = net;
            }

            Task MyStatsServiceInterface.AddAsync(string key, float value, CancellationToken cancellationToken)
            {
                MyPlatform.EnsureStatsKey(key);
                return SendWithoutContentAsync(OperationEnum.AddStats, new AnonymousServer.StatsRequestArgument() { FloatValue = value, Key = key, Type = AnonymousServer.StatsTypeEnum.Float }, cancellationToken);
            }

            Task MyStatsServiceInterface.AddAsync(string key, int value, CancellationToken cancellationToken)
            {
                MyPlatform.EnsureStatsKey(key);
                return SendWithoutContentAsync(OperationEnum.AddStats, new AnonymousServer.StatsRequestArgument() { IntValue = value, Key = key, Type = AnonymousServer.StatsTypeEnum.Int }, cancellationToken);
            }

            Task MyStatsServiceInterface.EnsureAsync(string definitionsJson, CancellationToken cancellationToken)
            {
                return MyPlatform.EnsureStatsAsync(this, definitionsJson, cancellationToken);
            }

            Task MyStatsServiceInterface.EnsureAsync(string key, float defaultValue, CancellationToken cancellationToken)
            {
                MyPlatform.EnsureStatsKey(key);
                return SendWithoutContentAsync(OperationEnum.EnsureStats, new AnonymousServer.StatsRequestArgument() { FloatValue = defaultValue, Key = key, Type = AnonymousServer.StatsTypeEnum.Float }, cancellationToken);
            }

            Task MyStatsServiceInterface.EnsureAsync(string key, int defaultValue, CancellationToken cancellationToken)
            {
                MyPlatform.EnsureStatsKey(key);
                return SendWithoutContentAsync(OperationEnum.EnsureStats, new AnonymousServer.StatsRequestArgument() { IntValue = defaultValue, Key = key, Type = AnonymousServer.StatsTypeEnum.Int }, cancellationToken);
            }

            Task MyStatsServiceInterface.EnsureAverageRateAsync(string key, float defaultValue, double windowSeconds, CancellationToken cancellationToken)
            {
                MyPlatform.EnsureStatsKey(key);
                return SendWithoutContentAsync(OperationEnum.EnsureStats, new AnonymousServer.StatsRequestArgument() { AverageRateWindowSeconds = windowSeconds, FloatValue = defaultValue, Key = key, Type = AnonymousServer.StatsTypeEnum.AverageRate }, cancellationToken);
            }

            Task MyStatsServiceInterface.ResetAsync(CancellationToken cancellationToken)
            {
                return SendWithoutContentAsync(OperationEnum.ResetStats, null, cancellationToken);
            }

            Task MyStatsServiceInterface.ResetAsync(string key, CancellationToken cancellationToken)
            {
                MyPlatform.EnsureStatsKey(key);
                return SendWithoutContentAsync(OperationEnum.ResetStat, new AnonymousServer.StatsRequestArgument() { Key = key }, cancellationToken);
            }

            Task MyStatsServiceInterface.UpdateAverageRateAsync(string key, float count, double sessionLengthSeconds, CancellationToken cancellationToken)
            {
                MyPlatform.EnsureStatsKey(key);
                return SendWithoutContentAsync(OperationEnum.UpdateAverageRateStat, new AnonymousServer.StatsRequestArgument() { AverageRateSessionLengthSeconds = sessionLengthSeconds, FloatValue = count, Key = key, Type = AnonymousServer.StatsTypeEnum.AverageRate }, cancellationToken);
            }

            private async Task<AnonymousServerResponse> SendAsync(OperationEnum operation, object argument, CancellationToken callerCancellationToken)
            {
                using (var cancellationSource = _net.CreateCancellationSource(callerCancellationToken))
                {
                    var cancellationToken = cancellationSource.Token;
                    await _gate.WaitAsync(cancellationToken);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await _net.SendAsync(operation, argument, _net.LifetimeCancellationToken);
                        var response = await _net.ReceiveAsync(operation, _net.LifetimeCancellationToken);
                        response.EnsureSuccess();
                        cancellationToken.ThrowIfCancellationRequested();
                        return response;
                    }
                    finally
                    {
                        _gate.Release();
                    }
                }
            }

            private async Task SendWithoutContentAsync(OperationEnum operation, object argument, CancellationToken cancellationToken)
            {
                await SendAsync(operation, argument, cancellationToken);
            }
        }

        internal const int Port = 45831;

        private readonly AnonymousClient Client;
        private readonly AnonymousNetChatService ChatService;
        private readonly SemaphoreSlim AuthenticationGate = new(1, 1);
        private readonly AnonymousNetFriendService _friendService;
        private readonly AnonymousNetHostService HostService;
        private readonly CancellationTokenSource LifetimeCancellationSource = new();
        private readonly CancellationToken LifetimeCancellationToken;
        internal readonly AnonymousNetLobbyService LobbyService;
        private readonly AnonymousNetMemberService MemberService;
        private readonly AnonymousNetPlayerService PlayerService;
        private readonly AnonymousNetRoomService RoomService;
        private readonly AnonymousServer Server = new();
        private readonly AnonymousStatsService _statsService;

        private string _account;
        private uint _appId;
        private string _currentRoomId;
        private bool _hasRemoteMember;
        private bool _isAuthenticated;
        private bool _isInitialized;
        private string _nickname;
        private RoomRoleEnum _roomRole;
        private bool _useLocal;

        MyNetChatServiceInterface MyNetInterface.Chat => ChatService;
        MyNetFriendServiceInterface MyNetInterface.Friend => _friendService;
        MyNetHostServiceInterface MyNetInterface.Host => HostService;
        MyNetLobbyServiceInterface MyNetInterface.Lobby => LobbyService;
        MyNetMemberServiceInterface MyNetInterface.Member => MemberService;
        MyNetPlayerServiceInterface MyNetInterface.Player => PlayerService;
        MyNetRoomServiceInterface MyNetInterface.Room => RoomService;
        bool MyNetInterface.UseLocal
        {
            get => _useLocal;
            set => _useLocal = value;
        }

        internal string Account => _account;
        internal MyNetChatResultInterface ChatResult { get; private set; }
        internal MyNetFriendResultInterface FriendResult { get; private set; }
        internal bool HasCurrentRoom => _roomRole != RoomRoleEnum.None;
        internal MyNetHostResultInterface HostResult { get; private set; }
        internal MyNetMemberResultInterface MemberResult { get; private set; }
        internal MyNetPlayerServiceInterface.UpdateResultInterface PlayerResult { get; private set; }
        internal MyNetRoomServiceInterface.UpdateResultInterface RoomResult { get; private set; }
        public MyStatsServiceInterface Stats => _statsService;
        internal MyTimeServiceInterface Time { get; private set; }
        internal bool UseLocal => _useLocal;

        internal AnonymousNet()
        {
            LifetimeCancellationToken = LifetimeCancellationSource.Token;
            Client = new AnonymousClient(LifetimeCancellationToken);
            ChatService = new(this);
            _friendService = new(this);
            HostService = new(this);
            LobbyService = new AnonymousNetLobbyService(this);
            MemberService = new(this);
            PlayerService = new AnonymousNetPlayerService(this);
            RoomService = new AnonymousNetRoomService(this);
            _statsService = new(this);
        }

        internal async Task AuthenticateAsync(string account, string nickname, uint appId, CancellationToken callerCancellationToken)
        {
            _account = account;
            _appId = appId;
            _nickname = nickname;
            await EnsureAuthenticatedAsync(callerCancellationToken);
        }

        private async Task EnsureAuthenticatedAsync(CancellationToken callerCancellationToken)
        {
            using (var cancellationSource = CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                await AuthenticationGate.WaitAsync(cancellationToken);
                try
                {
                    if (_isAuthenticated)
                        return;

                    Client.Shutdown();
                    Server.Start(cancellationToken);
                    await Client.ConnectAsync(cancellationToken);

                    var content = await Task.Run(() => MyNetSerializer.Serialize(new AnonymousServerAuthenticate.RequestArgument()
                    {
                        Account = _account,
                        AppId = _appId,
                        Nickname = _nickname,
                    }));
                    cancellationToken.ThrowIfCancellationRequested();
                    Client.Send(OperationEnum.Authenticate, content);
                    var response = await Client.ReceiveAsync(OperationEnum.Authenticate, cancellationToken);
                    response.EnsureSuccess();
                    _isAuthenticated = true;
                }
                catch
                {
                    Client.Shutdown();
                    throw;
                }
                finally
                {
                    AuthenticationGate.Release();
                }
            }
        }

        internal void ClearCurrentRoom(string roomId = null)
        {
            if ((roomId != null) && (_currentRoomId != roomId))
                return;

            _currentRoomId = null;
            _hasRemoteMember = false;
            _roomRole = RoomRoleEnum.None;
        }

        internal void ClearCurrentRoomForPlayer(string roomId, string playerId)
        {
            if (_account == playerId)
                ClearCurrentRoom(roomId);
        }

        internal CancellationTokenSource CreateCancellationSource(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LifetimeCancellationToken.ThrowIfCancellationRequested();
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, LifetimeCancellationToken);
        }

        internal void HandleDisconnected()
        {
            _isAuthenticated = false;
            Client.Shutdown();
            ClearCurrentRoom();
        }

        internal async Task<MyNetRoomInterface> GetCurrentRoomAsync(CancellationToken callerCancellationToken)
        {
            using (var cancellationSource = CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                await SendAsync(OperationEnum.GetCurrentRoom, null, cancellationToken);
                var response = await ReceiveAsync(OperationEnum.GetCurrentRoom, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (response.ResultCode == AnonymousServerResponse.ResultCodeEnum.NotFound)
                {
                    ClearCurrentRoom();
                    return null;
                }

                response.EnsureSuccess();
                var roomData = await response.GetContentAsync<AnonymousServerRoom.RoomData>();
                cancellationToken.ThrowIfCancellationRequested();
                if (roomData == null)
                    throw new FormatException("Invalid anonymous current room response.");

                var room = roomData.ToNetRoom();
                SetCurrentRoom(room);
                return room;
            }
        }

        private void HandleLocalMessages()
        {
            while (HostService.TryDequeue(out var response))
                MemberService.Receive(response);

            MemberService.HandleResponses();

            while (MemberService.TryDequeue(out var request))
                HostService.Receive(request);

            HostService.HandleRequests();
        }

        internal void Initialize(string account, MyNetChatResultInterface chatResult, MyNetFriendResultInterface friendResult, MyNetHostResultInterface hostResult, MyNetMemberResultInterface memberResult, MyNetPlayerServiceInterface.UpdateResultInterface playerResult, MyNetRoomServiceInterface.UpdateResultInterface roomResult, MyTimeServiceInterface time)
        {
            _account = account;
            ChatResult = chatResult ?? throw new ArgumentNullException(nameof(chatResult));
            FriendResult = friendResult ?? throw new ArgumentNullException(nameof(friendResult));
            HostResult = hostResult;
            MemberResult = memberResult;
            PlayerResult = playerResult;
            RoomResult = roomResult;
            Time = time ?? throw new ArgumentNullException(nameof(time));
            _isInitialized = true;
        }

        internal Task<AnonymousServerResponse> ReceiveAsync(OperationEnum operation, CancellationToken cancellationToken)
        {
            return Client.ReceiveAsync(operation, cancellationToken);
        }

        internal async Task RunServiceLoopAsync(CancellationToken cancellationToken)
        {
            while (cancellationToken.IsCancellationRequested == false)
            {
                if (_isInitialized)
                {
                    _friendService.Update();
                    while (Client.TryReceiveFriendInvite(out var friendInviteContent))
                    {
                        var friendInvite = await AnonymousServer.DeserializeAsync<AnonymousServer.FriendInviteData>(friendInviteContent);
                        if ((friendInvite == null) || string.IsNullOrWhiteSpace(friendInvite.PlayerId) || string.IsNullOrWhiteSpace(friendInvite.RoomId))
                            throw new FormatException("Invalid anonymous friend invitation.");

                        FriendResult.OnInvited(friendInvite.PlayerId, friendInvite.RoomId);
                    }

                    while (Client.TryReceiveChat(out var chatContent))
                    {
                        var chat = await AnonymousServer.DeserializeAsync<AnonymousServerChat.MessageData>(chatContent);
                        ChatResult.OnReceived(chat.Message, chat.PlayerId, chat.RoomId, chat.SentAt);
                    }

                    while (Client.TryReceiveRoomChanged(out var exitedRoomId, out var updatedContent))
                    {
                        if (exitedRoomId != null)
                        {
                            var isCurrentRoom = _currentRoomId == exitedRoomId;
                            ClearCurrentRoom(exitedRoomId);
                            if (isCurrentRoom)
                                RoomResult.OnFailed(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);

                            continue;
                        }

                        var roomData = await AnonymousServer.DeserializeAsync<AnonymousServerRoom.RoomData>(updatedContent);
                        if (roomData == null)
                            throw new FormatException("Invalid anonymous room update notification.");

                        var updatedRoom = roomData.ToNetRoom();
                        _hasRemoteMember = updatedRoom.PlayerCount > 1;
                        RoomResult.OnOk(updatedRoom);
                    }

                    var roomRole = _roomRole;

                    if (roomRole != RoomRoleEnum.None)
                    {
                        while (Client.TryReceivePlayerUpdated(out var content))
                        {
                            var playerData = await AnonymousServer.DeserializeAsync<AnonymousServerRoom.PlayerData>(content);
                            if (playerData == null)
                                throw new FormatException("Invalid anonymous player update notification.");

                            PlayerResult.OnOk(playerData.ToNetPlayer());
                        }

                        if (_useLocal)
                        {
                            HandleLocalMessages();
                            await Task.Delay(1, cancellationToken);
                            continue;
                        }

                        if (roomRole == RoomRoleEnum.Host)
                        {
                            // 서버 -> 클라 응답 전송
                            while (HostService.TryDequeue(out var response))
                            {
                                // 나에게는 즉시 수행
                                MemberService.Receive(response);

                                // 나를 제외한 멤버들에게 동기화
                                if (_hasRemoteMember)
                                    Client.SendHostResponse(MyNetSerializer.Serialize(response));
                            }
                        }
                        else
                        {
                            while (Client.TryReceiveHostResponse(out var content))
                                MemberService.Receive(await AnonymousServer.DeserializeAsync<MyNetResponse>(content));
                        }

                        MemberService.HandleResponses();

                        // 의도적으로 요청은 응답보다 늦게 처리하는 것이다.
                        // 클라 -> 서버 요청 적재
                        if (roomRole == RoomRoleEnum.Host)
                        {
                            // 나에게는 즉시 수행
                            while (MemberService.TryDequeue(out var request))
                                HostService.Receive(request);

                            // 나에게 날아온 요청들 적재
                            while (Client.TryReceiveMemberRequest(out var content))
                                HostService.Receive(await AnonymousServer.DeserializeAsync<MyNetRequest>(content));
                        }
                        else
                        {
                            // 호스트에게 요청
                            while (MemberService.TryDequeue(out var request))
                                Client.SendMemberRequest(MyNetSerializer.Serialize(request));
                        }

                        HostService.HandleRequests();
                    }
                    else if (_useLocal)
                    {
                        HandleLocalMessages();
                    }
                }

                await Task.Delay(1, cancellationToken);
            }
        }

        internal async Task SendAsync(OperationEnum operation, object argument, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureAuthenticatedAsync(cancellationToken);
            var content = argument == null ? Array.Empty<byte>() : await Task.Run(() => MyNetSerializer.Serialize(argument));
            cancellationToken.ThrowIfCancellationRequested();
            Client.Send(operation, content);
        }

        internal void SetCurrentRoom(MyNetRoomInterface room)
        {
            _currentRoomId = room.Id;
            _hasRemoteMember = room.PlayerCount > 1;
            _roomRole = room.HostId == _account ? RoomRoleEnum.Host : RoomRoleEnum.Member;
        }

        internal void Shutdown()
        {
            _friendService.StopPolling();
            LifetimeCancellationSource.Cancel();
            _isAuthenticated = false;
            Client.Shutdown();
            Server.Shutdown();
        }
    }
}
