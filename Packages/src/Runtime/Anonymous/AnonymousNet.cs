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
            ExitRoom = 3,
            GetRooms = 4,
            JoinRoom = 5,
            UpdatePlayer = 6,
            UpdateRoom = 7,
            GetCurrentRoom = 8,
        }

        internal const int Port = 45831;

        private readonly AnonymousClient Client;
        private readonly AnonymousNetHostService HostService;
        private readonly CancellationTokenSource LifetimeCancellationSource = new();
        private readonly CancellationToken LifetimeCancellationToken;
        internal readonly AnonymousNetLobbyService LobbyService;
        private readonly AnonymousNetMemberService MemberService;
        private readonly AnonymousNetPlayerService PlayerService;
        private readonly AnonymousNetRoomService RoomService;
        private readonly AnonymousServer Server = new();

        private string _account;
        private bool _isInitialized;

        MyNetHostServiceInterface MyNetInterface.Host => HostService;
        MyNetLobbyServiceInterface MyNetInterface.Lobby => LobbyService;
        MyNetMemberServiceInterface MyNetInterface.Member => MemberService;
        MyNetPlayerServiceInterface MyNetInterface.Player => PlayerService;
        MyNetRoomServiceInterface MyNetInterface.Room => RoomService;

        internal MyNetHostResultInterface HostResult { get; private set; }
        internal MyNetMemberResultInterface MemberResult { get; private set; }

        internal AnonymousNet()
        {
            HostService = new(this);
            // 순서 존나 맘에 안 드네
            LifetimeCancellationToken = LifetimeCancellationSource.Token;
            Client = new AnonymousClient(LifetimeCancellationToken);
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
                    return null;

                response.EnsureSuccess();
                var roomData = await response.GetContentAsync<AnonymousServerRoom.RoomData>();
                cancellationToken.ThrowIfCancellationRequested();
                if (roomData == null)
                    throw new FormatException("Invalid anonymous current room response.");

                return roomData.ToNetRoom();
            }
        }

        internal Task<AnonymousServerResponse> ReceiveAsync(OperationEnum operation, CancellationToken cancellationToken)
        {
            return Client.ReceiveAsync(operation, cancellationToken);
        }

        internal void Initialize(string account, MyNetHostResultInterface hostResult, MyNetMemberResultInterface memberResult)
        {
            _account = account;
            HostResult = hostResult;
            MemberResult = memberResult;
            _isInitialized = true;
        }

        internal async Task RunServiceLoopAsync(CancellationToken cancellationToken)
        {
            while (cancellationToken.IsCancellationRequested == false)
            {
                if (_isInitialized)
                {
                    var room = await GetCurrentRoomAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (room != null)
                    {
                        if (room.HostId == _account)
                        {
                            // 서버 -> 클라 응답 전송
                            while (HostService.TryDequeue(out var response))
                            {
                                // 나에게는 즉시 수행
                                MemberService.Receive(response);

                                // 나를 제외한 멤버들에게 동기화
                                if (room.PlayerCount > 1)
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
                        if (room.HostId == _account)
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

        internal void Shutdown()
        {
            LifetimeCancellationSource.Cancel();
            Client.Shutdown();
            Server.Shutdown();
        }
    }
}
