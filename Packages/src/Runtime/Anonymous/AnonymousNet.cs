using oojjrs.oplat.anonymous.controllers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNet : MyNetInterface
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(1);

        private readonly AnonymousClient Client;
        internal readonly AnonymousNetHostService HostService;
        private readonly CancellationTokenSource LifetimeCancellationSource = new();
        private readonly CancellationToken LifetimeCancellationToken;
        private readonly AnonymousNetLobbyService LobbyService;
        internal readonly AnonymousNetMemberService MemberService;
        private readonly AnonymousNetPlayerService PlayerService;
        private readonly AnonymousNetRoomService RoomService;
        private readonly AnonymousServer Server = new();
        private readonly object StateLock = new();

        private bool _isShutdown;

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
                try
                {
                    await Server.StartAsync(cancellationToken);
                    await ConnectAsync(cancellationToken);

                    var response = await SendAsync(AnonymousTransport.OperationEnum.Authenticate, new AnonymousServerAuthenticate.RequestArgument()
                    {
                        Account = account,
                        Nickname = nickname,
                    }, cancellationToken);
                    response.EnsureSuccess();
                }
                catch (Exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }
            }
        }

        internal CancellationTokenSource CreateCancellationSource(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LifetimeCancellationToken.ThrowIfCancellationRequested();
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, LifetimeCancellationToken);
        }

        private async Task ConnectAsync(CancellationToken cancellationToken)
        {
            using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutSource.CancelAfter(RequestTimeout);
                try
                {
                    await Client.ConnectAsync(timeoutSource.Token);
                }
                catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested == false)
                {
                    throw new TimeoutException("Anonymous connection timed out.", exception);
                }
            }
        }

        internal async Task<AnonymousServerResponse> SendAsync(AnonymousTransport.OperationEnum operation, object argument, CancellationToken cancellationToken)
        {
            using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutSource.CancelAfter(RequestTimeout);
                try
                {
                    return await Client.SendAsync(operation, argument, timeoutSource.Token);
                }
                catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested == false)
                {
                    throw new TimeoutException($"Anonymous request timed out: {operation}.", exception);
                }
            }
        }

        internal void Shutdown()
        {
            lock (StateLock)
            {
                if (_isShutdown)
                    return;

                _isShutdown = true;
            }

            LifetimeCancellationSource.Cancel();
            try
            {
                Client.Shutdown();
            }
            finally
            {
                Server.Shutdown();
            }
        }
    }
}
