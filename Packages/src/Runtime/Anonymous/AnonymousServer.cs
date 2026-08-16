using oojjrs.oplat.anonymous.controllers;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousServer
    {
        private readonly CancellationTokenSource LifetimeCancellationSource = new();
        private readonly TcpListener Listener = new(IPAddress.Loopback, AnonymousNet.Port);
        private readonly AnonymousServerRoom.State RoomState = new();

        internal static Task<T> DeserializeAsync<T>(byte[] content)
        {
            return Task.Run(() =>
            {
                using (var stream = new MemoryStream(content))
                    return (T)MyNetDeserializer.Deserialize(stream);
            });
        }

        private async Task AcceptAsync()
        {
            while (LifetimeCancellationSource.IsCancellationRequested == false)
            {
                var client = await Listener.AcceptTcpClientAsync();
                client.NoDelay = true;
                _ = RunConnectionAsync(client);
            }
        }

        private async Task<(AnonymousServerResponse Response, AnonymousServerSession Session)> CreateResponseAsync(AnonymousNet.OperationEnum operation, byte[] content, AnonymousServerSession session)
        {
            if (operation == AnonymousNet.OperationEnum.Authenticate)
            {
                if (session != null)
                    return (AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Conflict), session);

                return (AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Success), await AnonymousServerAuthenticate.RunAsync(content));
            }

            if (session == null)
                return (AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Unauthenticated), session);

            var response = operation switch
            {
                AnonymousNet.OperationEnum.CreateRoom => await AnonymousServerCreateRoom.RunAsync(content, RoomState, session),
                AnonymousNet.OperationEnum.ExitRoom => await AnonymousServerExitRoom.RunAsync(content, RoomState, session),
                AnonymousNet.OperationEnum.GetCurrentRoom => await AnonymousServerGetCurrentRoom.RunAsync(RoomState, session),
                AnonymousNet.OperationEnum.GetRequests => await AnonymousServerGetRequests.RunAsync(RoomState, session),
                AnonymousNet.OperationEnum.GetResponses => await AnonymousServerGetResponses.RunAsync(RoomState, session),
                AnonymousNet.OperationEnum.GetRooms => await AnonymousServerGetRooms.RunAsync(RoomState),
                AnonymousNet.OperationEnum.JoinRoom => await AnonymousServerJoinRoom.RunAsync(content, RoomState, session),
                AnonymousNet.OperationEnum.UpdatePlayer => await AnonymousServerUpdatePlayer.RunAsync(content, RoomState, session),
                AnonymousNet.OperationEnum.UpdateRoom => await AnonymousServerUpdateRoom.RunAsync(content, RoomState, session),
                _ => AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.UnsupportedOperation),
            };
            return (response, session);
        }

        private async Task RunConnectionAsync(TcpClient client)
        {
            using (var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(LifetimeCancellationSource.Token))
            {
                using (client)
                {
                    var cancellationToken = cancellationSource.Token;
                    var messages = new AnonymousTransport.MessageQueue(client.GetStream(), cancellationToken);
                    var session = default(AnonymousServerSession);
                    try
                    {
                        while (cancellationToken.IsCancellationRequested == false)
                        {
                            var message = await messages.ReceiveAsync(value => (value.Type == AnonymousTransport.Message.TypeEnum.Operation) || (value.Type == AnonymousTransport.Message.TypeEnum.MemberRequest) || (value.Type == AnonymousTransport.Message.TypeEnum.HostResponse), cancellationToken);
                            if (message == null)
                                return;

                            if (message.Type == AnonymousTransport.Message.TypeEnum.HostResponse)
                            {
                                await AnonymousServerAddResponse.RunAsync(message.Content, RoomState, session);
                            }
                            else if (message.Type == AnonymousTransport.Message.TypeEnum.MemberRequest)
                            {
                                await AnonymousServerAddRequest.RunAsync(message.Content, RoomState, session);
                            }
                            else
                            {
                                var response = await CreateResponseAsync(message.Operation, message.Content, session);
                                session = response.Session;
                                messages.Send(AnonymousTransport.Message.CreateOperationResult(message.Operation, response.Response));
                            }
                        }
                    }
                    finally
                    {
                        cancellationSource.Cancel();
                    }
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
