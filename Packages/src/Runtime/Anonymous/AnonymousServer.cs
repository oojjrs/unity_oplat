using oojjrs.oplat.anonymous.controllers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousServer
    {
        public record AddFriendRequestArgument
        {
            public string PlayerId { get; set; }
        }

        public record FriendData
        {
            public string Id { get; set; }
            public string Nickname { get; set; }
            public string RoomId { get; set; }
            public MyNetFriendInterface.StateEnum State { get; set; }
        }

        public record FriendsResponseArgument
        {
            public FriendData[] Friends { get; set; }
        }

        private readonly AnonymousServerChat.State ChatState = new();
        private readonly object _friendStorageLock = new();
        private readonly CancellationTokenSource LifetimeCancellationSource = new();
        private readonly TcpListener Listener = new(IPAddress.Loopback, AnonymousNet.Port);
        private readonly AnonymousServerRoom.State RoomState = new();
        private readonly Dictionary<string, AnonymousServerSession> Sessions = new();

        private static void AddFriendAccount(AnonymousServerSession session, string playerId)
        {
            var accounts = ReadFriendAccounts(session);
            if (accounts.Contains(playerId, StringComparer.Ordinal))
                return;

            var path = GetFriendStoragePath(session);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = File.Create(temporaryPath))
                {
                    new DataContractJsonSerializer(typeof(string[])).WriteObject(stream, accounts.Append(playerId).ToArray());
                    stream.Flush(true);
                }

                if (File.Exists(path))
                    File.Replace(temporaryPath, path, null);
                else
                    File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        internal static Task<T> DeserializeAsync<T>(byte[] content)
        {
            return Task.Run(() =>
            {
                using (var stream = new MemoryStream(content))
                    return (T)MyNetDeserializer.Deserialize(stream);
            });
        }

        private static string GetFriendStorageKey(string value)
        {
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string GetFriendStoragePath(AnonymousServerSession session)
        {
            var localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localApplicationDataPath))
                throw new InvalidOperationException("The local application data path is unavailable.");

            return Path.Combine(localApplicationDataPath, "oojjrs", "Oplat", "AnonymousServer", "v1", GetFriendStorageKey(session.ProjectKey), session.AppId.ToString(CultureInfo.InvariantCulture), "users", GetFriendStorageKey(session.Account), "friends.json");
        }

        private static string[] ReadFriendAccounts(AnonymousServerSession session)
        {
            try
            {
                using (var stream = File.OpenRead(GetFriendStoragePath(session)))
                {
                    var accounts = (string[])new DataContractJsonSerializer(typeof(string[])).ReadObject(stream);
                    if ((accounts == null) || accounts.Any(string.IsNullOrEmpty))
                        throw new FormatException("Invalid anonymous friend accounts.");

                    return accounts;
                }
            }
            catch (FileNotFoundException)
            {
                return Array.Empty<string>();
            }
            catch (DirectoryNotFoundException)
            {
                return Array.Empty<string>();
            }
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

        private async Task<AnonymousServerResponse> AddFriendAsync(byte[] content, AnonymousServerSession session)
        {
            var cancellationToken = LifetimeCancellationSource.Token;
            try
            {
                var argument = await DeserializeAsync<AddFriendRequestArgument>(content);
                if ((argument == null) || string.IsNullOrWhiteSpace(argument.PlayerId))
                    return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

                await Task.Run(() =>
                {
                    lock (_friendStorageLock)
                        AddFriendAccount(session, argument.PlayerId);
                }, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Success);
            }
            catch (Exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.ServerError);
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
                AnonymousNet.OperationEnum.AddFriend => await AddFriendAsync(content, session),
                AnonymousNet.OperationEnum.CreateRoom => await AnonymousServerCreateRoom.RunAsync(content, RoomState, session),
                AnonymousNet.OperationEnum.ExitChat => await AnonymousServerExitChat.RunAsync(content, ChatState, session),
                AnonymousNet.OperationEnum.ExitRoom => await AnonymousServerExitRoom.RunAsync(content, ChatState, RoomState, Sessions, session),
                AnonymousNet.OperationEnum.GetCurrentRoom => await AnonymousServerGetCurrentRoom.RunAsync(RoomState, session),
                AnonymousNet.OperationEnum.GetFriends => await GetFriendsAsync(session),
                AnonymousNet.OperationEnum.GetRooms => await AnonymousServerGetRooms.RunAsync(RoomState),
                AnonymousNet.OperationEnum.JoinChat => await AnonymousServerJoinChat.RunAsync(content, ChatState, RoomState, session),
                AnonymousNet.OperationEnum.JoinRoom => await AnonymousServerJoinRoom.RunAsync(content, RoomState, Sessions, session),
                AnonymousNet.OperationEnum.SendChat => await AnonymousServerSendChat.RunAsync(content, ChatState, RoomState, Sessions, session),
                AnonymousNet.OperationEnum.UpdatePlayer => await AnonymousServerUpdatePlayer.RunAsync(content, RoomState, Sessions, session),
                AnonymousNet.OperationEnum.UpdateRoom => await AnonymousServerUpdateRoom.RunAsync(content, RoomState, Sessions, session),
                _ => AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.UnsupportedOperation),
            };
            return (response, session);
        }

        private async Task<AnonymousServerResponse> GetFriendsAsync(AnonymousServerSession session)
        {
            var cancellationToken = LifetimeCancellationSource.Token;
            try
            {
                var accounts = await Task.Run(() =>
                {
                    lock (_friendStorageLock)
                        return ReadFriendAccounts(session);
                }, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var friends = new List<FriendData>();
                foreach (var account in accounts.Distinct(StringComparer.Ordinal))
                {
                    var friend = new FriendData()
                    {
                        Id = account,
                        Nickname = account,
                        RoomId = string.Empty,
                        State = MyNetFriendInterface.StateEnum.Offline,
                    };
                    if (Sessions.TryGetValue(account, out var friendSession) && (friendSession.AppId == session.AppId) && (friendSession.ProjectKey == session.ProjectKey))
                    {
                        friend.Nickname = friendSession.Nickname;
                        friend.State = MyNetFriendInterface.StateEnum.Online;
                        var room = RoomState.Rooms.Find(value => (value.Room.IsPrivate == false) && value.Room.Players.Any(player => player.Id == account));
                        friend.RoomId = room?.Room.Id ?? string.Empty;
                    }

                    friends.Add(friend);
                }

                return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, new FriendsResponseArgument() { Friends = friends.ToArray() });
            }
            catch (Exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.ServerError);
            }
        }

        private async Task RemoveSessionAsync(AnonymousServerSession session)
        {
            if (session == null)
                return;

            if (Sessions.TryGetValue(session.Account, out var currentSession) && ReferenceEquals(currentSession, session))
                Sessions.Remove(session.Account);

            ChatState.Remove(session.Account);

            var roomIndex = RoomState.Rooms.FindIndex(secret => (secret.Room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>()).Any(player => player.Id == session.Account));
            if (roomIndex < 0)
                return;

            var room = RoomState.Rooms[roomIndex];
            if (room.Room.HostId == session.Account)
            {
                foreach (var player in room.Room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>())
                    ChatState.Remove(player.Id, room.Room.Id);

                AnonymousServerRoom.NotifyExited(room.Room, Sessions, session.Account);
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
