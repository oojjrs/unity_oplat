using oojjrs.oplat.anonymous.controllers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousServer
    {
        private readonly CancellationTokenSource LifetimeCancellationSource = new();
        private readonly TcpListener Listener = new(IPAddress.Loopback, AnonymousTransport.Port);
        private readonly AnonymousServerRoom.State RoomState = new();

        private async Task AcceptAsync()
        {
            while (LifetimeCancellationSource.IsCancellationRequested == false)
            {
                var client = await Listener.AcceptTcpClientAsync();
                client.NoDelay = true;
                _ = RunConnectionAsync(client);
            }
        }

        private async Task<(AnonymousServerResponse Response, AnonymousServerSession Session)> CreateResponseAsync(AnonymousTransport.OperationEnum operation, byte[] content, AnonymousServerSession session)
        {
            if (operation == AnonymousTransport.OperationEnum.Authenticate)
            {
                if (session != null)
                    return (AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Conflict), session);

                return (AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Success), await AnonymousServerAuthenticate.RunAsync(content));
            }

            if (session == null)
                return (AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Unauthenticated), session);

            var response = operation switch
            {
                AnonymousTransport.OperationEnum.CreateRoom => await AnonymousServerCreateRoom.RunAsync(content, RoomState, session),
                AnonymousTransport.OperationEnum.ExitRoom => await AnonymousServerExitRoom.RunAsync(content, RoomState, session),
                AnonymousTransport.OperationEnum.GetRooms => await AnonymousServerGetRooms.RunAsync(RoomState),
                AnonymousTransport.OperationEnum.JoinRoom => await AnonymousServerJoinRoom.RunAsync(content, RoomState, session),
                AnonymousTransport.OperationEnum.UpdatePlayer => await AnonymousServerUpdatePlayer.RunAsync(content, RoomState, session),
                AnonymousTransport.OperationEnum.UpdateRoom => await AnonymousServerUpdateRoom.RunAsync(content, RoomState, session),
                _ => AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.UnsupportedOperation),
            };
            return (response, session);
        }

        private async Task RunConnectionAsync(TcpClient client)
        {
            using (client)
            {
                var session = default(AnonymousServerSession);
                var stream = client.GetStream();
                while (LifetimeCancellationSource.IsCancellationRequested == false)
                {
                    var request = await AnonymousTransport.ReadRequestAsync(stream, LifetimeCancellationSource.Token);
                    if (request == null)
                        return;

                    var response = await CreateResponseAsync(request.Value.Operation, request.Value.Content, session);
                    session = response.Session;
                    await AnonymousTransport.WriteResponseAsync(stream, response.Response, LifetimeCancellationSource.Token);
                }
            }
        }

        internal void Shutdown()
        {
            LifetimeCancellationSource.Cancel();
            Listener.Stop();
        }

        internal void Start(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Listener.Start();
            }
            catch (SocketException exception) when (exception.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                return;
            }

            _ = AcceptAsync();
        }
    }
}
