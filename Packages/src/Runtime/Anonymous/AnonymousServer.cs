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

        private AnonymousServerResponse CreateResponse(AnonymousTransport.OperationEnum operation, byte[] content, ref AnonymousServerSession session)
        {
            if (operation == AnonymousTransport.OperationEnum.Authenticate)
            {
                if (session != null)
                    return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Conflict);

                session = AnonymousServerAuthenticate.Run(content);
                return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Success);
            }

            if (session == null)
                return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Unauthenticated);

            return operation switch
            {
                AnonymousTransport.OperationEnum.CreateRoom => AnonymousServerCreateRoom.Run(content, RoomState, session),
                AnonymousTransport.OperationEnum.ExitRoom => AnonymousServerExitRoom.Run(content, RoomState, session),
                AnonymousTransport.OperationEnum.GetRooms => AnonymousServerGetRooms.Run(RoomState),
                AnonymousTransport.OperationEnum.JoinRoom => AnonymousServerJoinRoom.Run(content, RoomState, session),
                AnonymousTransport.OperationEnum.UpdatePlayer => AnonymousServerUpdatePlayer.Run(content, RoomState, session),
                AnonymousTransport.OperationEnum.UpdateRoom => AnonymousServerUpdateRoom.Run(content, RoomState, session),
                _ => AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.UnsupportedOperation),
            };
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

                    var response = CreateResponse(request.Value.Operation, request.Value.Content, ref session);
                    await AnonymousTransport.WriteResponseAsync(stream, response, LifetimeCancellationSource.Token);
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
