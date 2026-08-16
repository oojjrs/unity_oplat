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
            using (client)
            {
                var session = default(AnonymousServerSession);
                var stream = client.GetStream();
                while (LifetimeCancellationSource.IsCancellationRequested == false)
                {
                    var request = await AnonymousTransport.ReadAsync(stream, LifetimeCancellationSource.Token);
                    if (request == null)
                        return;

                    var response = await CreateResponseAsync((AnonymousNet.OperationEnum)request[0], request[1..], session);
                    session = response.Session;

                    var content = new byte[1 + response.Response.Content.Length];
                    content[0] = (byte)response.Response.ResultCode;
                    response.Response.Content.CopyTo(content, 1);

                    await AnonymousTransport.WriteAsync(stream, content, LifetimeCancellationSource.Token);
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
