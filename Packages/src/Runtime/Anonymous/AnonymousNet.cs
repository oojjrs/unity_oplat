using oojjrs.oplat.anonymous.controllers;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNet : MyNetInterface
    {
        private readonly AnonymousClient Client;
        internal readonly AnonymousNetHostService HostService;
        private readonly CancellationTokenSource LifetimeCancellationSource = new();
        private readonly CancellationToken LifetimeCancellationToken;
        private readonly AnonymousNetLobbyService LobbyService;
        internal readonly AnonymousNetMemberService MemberService;
        private readonly AnonymousNetPlayerService PlayerService;
        private readonly AnonymousNetRoomService RoomService;
        private readonly AnonymousServer Server = new();

        MyNetHostServiceInterface MyNetInterface.Host => HostService;
        MyNetLobbyServiceInterface MyNetInterface.Lobby => LobbyService;
        MyNetMemberServiceInterface MyNetInterface.Member => MemberService;
        MyNetPlayerServiceInterface MyNetInterface.Player => PlayerService;
        MyNetRoomServiceInterface MyNetInterface.Room => RoomService;

        internal MyNetHostResultInterface HostResult { get; set; }
        internal MyNetMemberResultInterface MemberResult { get; set; }

        internal AnonymousNet()
        {
            HostService = new(this);
            // 순서 존나 맘에 안 드네
            LifetimeCancellationToken = LifetimeCancellationSource.Token;
            Client = new AnonymousClient();
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

                var response = await SendAsync(AnonymousTransport.OperationEnum.Authenticate, new AnonymousServerAuthenticate.RequestArgument()
                {
                    Account = account,
                    Nickname = nickname,
                }, cancellationToken);
                response.EnsureSuccess();
            }
        }

        internal CancellationTokenSource CreateCancellationSource(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LifetimeCancellationToken.ThrowIfCancellationRequested();
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, LifetimeCancellationToken);
        }

        internal async Task<AnonymousServerResponse> SendAsync(AnonymousTransport.OperationEnum operation, object argument, CancellationToken cancellationToken)
        {
            return await Client.SendAsync(operation, await AnonymousTransport.SerializeAsync(argument), cancellationToken);
        }

        internal void Shutdown()
        {
            LifetimeCancellationSource.Cancel();
            Client.Shutdown();
            Server.Shutdown();
        }
    }
}
