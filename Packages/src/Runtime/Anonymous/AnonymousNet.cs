using oojjrs.oplat.anonymous.controllers;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNet : MyNetInterface
    {
        internal enum OperationEnum : byte
        {
            Authenticate = 1,
            CreateRoom = 2,
            ExitChat = 9,
            ExitRoom = 3,
            GetCurrentRoom = 8,
            GetRooms = 4,
            JoinChat = 10,
            JoinRoom = 5,
            SendChat = 11,
            UpdatePlayer = 6,
            UpdateRoom = 7,
        }

        private enum RoomRoleEnum : byte
        {
            Host = 1,
            Member = 2,
            None = 0,
        }

        internal const int Port = 45831;

        private readonly AnonymousClient Client;
        private readonly AnonymousNetChatService ChatService;
        private readonly AnonymousNetHostService HostService;
        private readonly CancellationTokenSource LifetimeCancellationSource = new();
        private readonly CancellationToken LifetimeCancellationToken;
        internal readonly AnonymousNetLobbyService LobbyService;
        private readonly AnonymousNetMemberService MemberService;
        private readonly AnonymousNetPlayerService PlayerService;
        private readonly AnonymousNetRoomService RoomService;
        private readonly AnonymousServer Server = new();

        private string _account;
        private string _currentRoomId;
        private bool _hasRemoteMember;
        private bool _isInitialized;
        private RoomRoleEnum _roomRole;
        private bool _useLocal;

        MyNetChatServiceInterface MyNetInterface.Chat => ChatService;
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
        internal bool HasCurrentRoom => _roomRole != RoomRoleEnum.None;
        internal MyNetHostResultInterface HostResult { get; private set; }
        internal MyNetMemberResultInterface MemberResult { get; private set; }
        internal MyNetPlayerServiceInterface.UpdateResultInterface PlayerResult { get; private set; }
        internal MyNetRoomServiceInterface.UpdateResultInterface RoomResult { get; private set; }
        internal bool UseLocal => _useLocal;

        internal AnonymousNet()
        {
            LifetimeCancellationToken = LifetimeCancellationSource.Token;
            Client = new AnonymousClient(LifetimeCancellationToken);
            ChatService = new(this);
            HostService = new(this);
            LobbyService = new AnonymousNetLobbyService(this);
            MemberService = new(this);
            PlayerService = new AnonymousNetPlayerService(this);
            RoomService = new AnonymousNetRoomService(this);
        }

        internal async Task AuthenticateAsync(string account, string nickname, CancellationToken callerCancellationToken)
        {
            using (var cancellationSource = CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                Server.Start(cancellationToken);
                await Client.ConnectAsync(cancellationToken);

                await SendAsync(OperationEnum.Authenticate, new AnonymousServerAuthenticate.RequestArgument()
                {
                    Account = account,
                    Nickname = nickname,
                }, cancellationToken);
                var response = await ReceiveAsync(OperationEnum.Authenticate, cancellationToken);
                response.EnsureSuccess();
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

        internal void Initialize(string account, MyNetChatResultInterface chatResult, MyNetHostResultInterface hostResult, MyNetMemberResultInterface memberResult, MyNetPlayerServiceInterface.UpdateResultInterface playerResult, MyNetRoomServiceInterface.UpdateResultInterface roomResult)
        {
            _account = account;
            ChatResult = chatResult ?? throw new ArgumentNullException(nameof(chatResult));
            HostResult = hostResult;
            MemberResult = memberResult;
            PlayerResult = playerResult;
            RoomResult = roomResult;
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
                    while (Client.TryReceiveChat(out var chatContent))
                    {
                        var chat = await AnonymousServer.DeserializeAsync<AnonymousServerChat.MessageData>(chatContent);
                        ChatResult.OnReceived(chat.Message, chat.PlayerId, chat.RoomId);
                    }

                    if (_useLocal)
                    {
                        HandleLocalMessages();
                        await Task.Delay(1, cancellationToken);
                        continue;
                    }

                    while (Client.TryReceiveRoomChanged(out var exitedRoomId, out var updatedContent))
                    {
                        if (exitedRoomId != null)
                        {
                            ClearCurrentRoom(exitedRoomId);
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
                }

                await Task.Delay(1, cancellationToken);
            }
        }

        internal async Task SendAsync(OperationEnum operation, object argument, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
            LifetimeCancellationSource.Cancel();
            Client.Shutdown();
            Server.Shutdown();
        }
    }
}
