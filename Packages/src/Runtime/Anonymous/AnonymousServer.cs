using oojjrs.oplat.anonymous.controllers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private readonly Dictionary<string, AnonymousServerSession> Sessions = new();

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

        private async Task<(AnonymousServerResponse Response, AnonymousServerSession Session)> CreateResponseAsync(AnonymousNet.OperationEnum operation, byte[] content, AnonymousServerSession session, AnonymousTransport.MessageQueue messages)
        {
            if (operation == AnonymousNet.OperationEnum.Authenticate)
            {
                if (session != null)
                    return (AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Conflict), session);

                var authenticatedSession = await AnonymousServerAuthenticate.RunAsync(content, messages);
                if (Sessions.ContainsKey(authenticatedSession.Account))
                    return (AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Conflict), null);

                Sessions.Add(authenticatedSession.Account, authenticatedSession);

                return (AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Success), authenticatedSession);
            }

            if (session == null)
                return (AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Unauthenticated), session);

            var response = operation switch
            {
                AnonymousNet.OperationEnum.CreateRoom => await AnonymousServerCreateRoom.RunAsync(content, RoomState, session),
                AnonymousNet.OperationEnum.ExitRoom => await AnonymousServerExitRoom.RunAsync(content, RoomState, Sessions, session),
                AnonymousNet.OperationEnum.GetCurrentRoom => await AnonymousServerGetCurrentRoom.RunAsync(RoomState, session),
                AnonymousNet.OperationEnum.GetRooms => await AnonymousServerGetRooms.RunAsync(RoomState),
                AnonymousNet.OperationEnum.JoinRoom => await AnonymousServerJoinRoom.RunAsync(content, RoomState, Sessions, session),
                AnonymousNet.OperationEnum.UpdatePlayer => await AnonymousServerUpdatePlayer.RunAsync(content, RoomState, Sessions, session),
                AnonymousNet.OperationEnum.UpdateRoom => await AnonymousServerUpdateRoom.RunAsync(content, RoomState, Sessions, session),
                _ => AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.UnsupportedOperation),
            };
            return (response, session);
        }

        private async Task RemoveSessionAsync(AnonymousServerSession session)
        {
            if (session == null)
                return;

            if (Sessions.TryGetValue(session.Account, out var currentSession) && ReferenceEquals(currentSession, session))
                Sessions.Remove(session.Account);

            var roomIndex = RoomState.Rooms.FindIndex(secret => (secret.Room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>()).Any(player => player.Id == session.Account));
            if (roomIndex < 0)
                return;

            var room = RoomState.Rooms[roomIndex];
            if (room.Room.HostId == session.Account)
            {
                RoomState.RoomCodes.Remove(room.Room.Code);
                RoomState.Rooms.RemoveAt(roomIndex);
                return;
            }

            room.Room.Players = room.Room.Players.Where(player => player.Id != session.Account).ToArray();
            await AnonymousServerRoom.NotifyUpdatedAsync(room.Room, Sessions, session.Account);
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
                                await AnonymousServerAddResponse.RunAsync(message.Content, RoomState, Sessions, session);
                            }
                            else if (message.Type == AnonymousTransport.Message.TypeEnum.MemberRequest)
                            {
                                await AnonymousServerAddRequest.RunAsync(message.Content, RoomState, Sessions, session);
                            }
                            else
                            {
                                var response = await CreateResponseAsync(message.Operation, message.Content, session, messages);
                                session = response.Session;
                                messages.Send(AnonymousTransport.Message.CreateOperationResult(message.Operation, response.Response));
                            }
                        }
                    }
                    finally
                    {
                        await RemoveSessionAsync(session);
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
