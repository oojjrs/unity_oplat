using oojjrs.oplat.anonymous.controllers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousServer
    {
        public enum StatsTypeEnum : byte
        {
            AverageRate = 3,
            Float = 2,
            Int = 1,
        }

        public record AddFriendRequestArgument
        {
            public string PlayerId { get; set; }
        }

        public record AchievementRequestArgument
        {
            public string DefinitionsJson { get; set; }
            public bool IncludeHidden { get; set; }
            public string Key { get; set; }
            public string Language { get; set; }
            public string StatKey { get; set; }
            public double TargetValue { get; set; }
        }

        public record AchievementResponseArgument
        {
            public string DefinitionsJson { get; set; }
            public bool IsUnlocked { get; set; }
        }

        public record FriendData
        {
            public string Id { get; set; }
            public string Nickname { get; set; }
            public string RoomId { get; set; }
            public MyNetFriendInterface.StateEnum State { get; set; }
        }

        public record FriendInviteData
        {
            public string PlayerId { get; set; }
            public string RoomId { get; set; }
        }

        public record FriendsResponseArgument
        {
            public FriendData[] Friends { get; set; }
        }

        public record InviteFriendRequestArgument
        {
            public string PlayerId { get; set; }
            public string RoomId { get; set; }
        }

        public record StatsRequestArgument
        {
            public double AverageRateSessionLengthSeconds { get; set; }
            public double AverageRateWindowSeconds { get; set; }
            public string DefinitionsJson { get; set; }
            public float FloatValue { get; set; }
            public int IntValue { get; set; }
            public string Key { get; set; }
            public string Language { get; set; }
            public StatsTypeEnum Type { get; set; }
        }

        public record StatsResponseArgument
        {
            public string DefinitionsJson { get; set; }
            public float FloatValue { get; set; }
            public double GlobalFloatValue { get; set; }
            public long GlobalIntValue { get; set; }
            public int IntValue { get; set; }
            public (string Key, double Value)[] Values { get; set; }
        }

        private readonly AnonymousServerChat.State ChatState = new();
        private readonly object _friendStorageLock = new();
        private readonly CancellationTokenSource LifetimeCancellationSource = new();
        private readonly TcpListener Listener = new(IPAddress.Loopback, AnonymousNet.Port);
        private readonly AnonymousServerRoom.State RoomState = new();
        private readonly Dictionary<string, AnonymousServerSession> Sessions = new();

        private bool _isStarted;

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

        private static string GetAccountDirectoryName(string account)
        {
            var value = Uri.EscapeDataString(account);
            if (value.EndsWith(".", StringComparison.Ordinal))
                value = value[..^1] + "%2E";

            return $"Account={value}";
        }

        private static string GetFriendStoragePath(AnonymousServerSession session)
        {
            return GetFriendStoragePath(session.AppId, session.Account);
        }

        public static string GetFriendStoragePath(uint appId, string account)
        {
            var localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localApplicationDataPath))
                throw new InvalidOperationException("The local application data path is unavailable.");

            return Path.Combine(localApplicationDataPath, "oojjrs", "Oplat", "AnonymousServer", "v1", $"AppId={appId.ToString(CultureInfo.InvariantCulture)}", "users", GetAccountDirectoryName(account), "friends.json");
        }

        public static Task<(bool IsFound, byte[] Data)> ReadProgressDataAsync(string path, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return (true, File.ReadAllBytes(path));
                }
                catch (FileNotFoundException)
                {
                    return (false, Array.Empty<byte>());
                }
                catch (DirectoryNotFoundException)
                {
                    return (false, Array.Empty<byte>());
                }
            }, cancellationToken);
        }

        public static Task WriteProgressDataAsync(string path, byte[] data, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var stream = File.Create(temporaryPath))
                    {
                        stream.Write(data, 0, data.Length);
                        stream.Flush(true);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
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
            }, cancellationToken);
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
                AnonymousNet.OperationEnum.AddStats => await HandleStatsAsync(operation, content, session),
                AnonymousNet.OperationEnum.CreateRoom => await AnonymousServerCreateRoom.RunAsync(content, RoomState, session),
                AnonymousNet.OperationEnum.EnsureAchievement => await HandleAchievementAsync(operation, content, session),
                AnonymousNet.OperationEnum.EnsureStats => await HandleStatsAsync(operation, content, session),
                AnonymousNet.OperationEnum.ExitChat => await AnonymousServerExitChat.RunAsync(content, ChatState, session),
                AnonymousNet.OperationEnum.ExitRoom => await AnonymousServerExitRoom.RunAsync(content, ChatState, RoomState, Sessions, session),
                AnonymousNet.OperationEnum.GetAverageRateStat => await HandleStatsAsync(operation, content, session),
                AnonymousNet.OperationEnum.GetAchievement => await HandleAchievementAsync(operation, content, session),
                AnonymousNet.OperationEnum.GetAchievementDefinition => await HandleAchievementAsync(operation, content, session),
                AnonymousNet.OperationEnum.GetAchievementDefinitions => await HandleAchievementAsync(operation, content, session),
                AnonymousNet.OperationEnum.GetCurrentRoom => await AnonymousServerGetCurrentRoom.RunAsync(RoomState, session),
                AnonymousNet.OperationEnum.GetFriends => await GetFriendsAsync(session),
                AnonymousNet.OperationEnum.GetFloatStat => await HandleStatsAsync(operation, content, session),
                AnonymousNet.OperationEnum.GetGlobalFloatStat => await HandleStatsAsync(operation, content, session),
                AnonymousNet.OperationEnum.GetGlobalIntStat => await HandleStatsAsync(operation, content, session),
                AnonymousNet.OperationEnum.GetIntStat => await HandleStatsAsync(operation, content, session),
                AnonymousNet.OperationEnum.GetRooms => await AnonymousServerGetRooms.RunAsync(RoomState),
                AnonymousNet.OperationEnum.GetStatDefinition => await HandleStatsAsync(operation, content, session),
                AnonymousNet.OperationEnum.GetStatDefinitions => await HandleStatsAsync(operation, content, session),
                AnonymousNet.OperationEnum.GetStatValues => await HandleStatsAsync(operation, content, session),
                AnonymousNet.OperationEnum.InviteFriend => await InviteFriendAsync(content, session),
                AnonymousNet.OperationEnum.JoinChat => await AnonymousServerJoinChat.RunAsync(content, ChatState, RoomState, session),
                AnonymousNet.OperationEnum.JoinRoom => await AnonymousServerJoinRoom.RunAsync(content, RoomState, Sessions, session),
                AnonymousNet.OperationEnum.ResetAchievement => await HandleAchievementAsync(operation, content, session),
                AnonymousNet.OperationEnum.ResetAchievements => await HandleAchievementAsync(operation, content, session),
                AnonymousNet.OperationEnum.ResetAllProgress => await ResetAllProgressAsync(session),
                AnonymousNet.OperationEnum.ResetStat => await HandleStatsAsync(operation, content, session),
                AnonymousNet.OperationEnum.ResetStats => await HandleStatsAsync(operation, content, session),
                AnonymousNet.OperationEnum.SendChat => await AnonymousServerSendChat.RunAsync(content, ChatState, RoomState, Sessions, session),
                AnonymousNet.OperationEnum.SetStats => await HandleStatsAsync(operation, content, session),
                AnonymousNet.OperationEnum.UnlockAchievement => await HandleAchievementAsync(operation, content, session),
                AnonymousNet.OperationEnum.UpdateAverageRateStat => await HandleStatsAsync(operation, content, session),
                AnonymousNet.OperationEnum.UpdatePlayer => await AnonymousServerUpdatePlayer.RunAsync(content, RoomState, Sessions, session),
                AnonymousNet.OperationEnum.UpdateRoom => await AnonymousServerUpdateRoom.RunAsync(content, RoomState, Sessions, session),
                _ => AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.UnsupportedOperation),
            };
            return (response, session);
        }

        private MyAchievementServiceInterface GetAchievementService(AnonymousServerSession session)
        {
            return MyAnonymousProgressServer.GetServices(session.AppId, session.Account, MyProgressDefinitions.WriterEnum.Client).Achievements;
        }

        private MyStatsServiceInterface GetStatsService(AnonymousServerSession session)
        {
            return MyAnonymousProgressServer.GetServices(session.AppId, session.Account, MyProgressDefinitions.WriterEnum.Client).Stats;
        }

        private async Task<AnonymousServerResponse> HandleAchievementAsync(AnonymousNet.OperationEnum operation, byte[] content, AnonymousServerSession session)
        {
            var cancellationToken = LifetimeCancellationSource.Token;
            try
            {
                var service = GetAchievementService(session);
                if (operation == AnonymousNet.OperationEnum.ResetAchievements)
                {
                    await service.ResetAsync(cancellationToken);
                    return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Success);
                }

                var argument = await DeserializeAsync<AchievementRequestArgument>(content);
                if (argument == null)
                    return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

                switch (operation)
                {
                    case AnonymousNet.OperationEnum.EnsureAchievement:
                        await service.EnsureAsync(argument.DefinitionsJson, cancellationToken);
                        break;
                    case AnonymousNet.OperationEnum.GetAchievementDefinition:
                        return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, new AchievementResponseArgument() { DefinitionsJson = new MyProgressDefinitions() { Achievements = new[] { await service.GetDefinitionAsync(argument.Key, argument.Language, cancellationToken) }, Stats = await GetStatsService(session).GetDefinitionsAsync(argument.Language, cancellationToken) }.ToJson() });
                    case AnonymousNet.OperationEnum.GetAchievementDefinitions:
                        return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, new AchievementResponseArgument() { DefinitionsJson = new MyProgressDefinitions() { Achievements = await service.GetDefinitionsAsync(argument.Language, argument.IncludeHidden, cancellationToken), Stats = await GetStatsService(session).GetDefinitionsAsync(argument.Language, cancellationToken) }.ToJson() });
                    case AnonymousNet.OperationEnum.GetAchievement:
                        return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, new AchievementResponseArgument() { IsUnlocked = await service.IsUnlockedAsync(argument.Key, cancellationToken) });
                    case AnonymousNet.OperationEnum.ResetAchievement:
                        await service.ResetAsync(argument.Key, cancellationToken);
                        break;
                    case AnonymousNet.OperationEnum.UnlockAchievement:
                        await service.UnlockAsync(argument.Key, cancellationToken);
                        break;
                    default:
                        return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.UnsupportedOperation);
                }

                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Success);
            }
            catch (UnauthorizedAccessException)
            {
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);
            }
            catch (Exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.ServerError);
            }
        }

        private async Task<AnonymousServerResponse> HandleStatsAsync(AnonymousNet.OperationEnum operation, byte[] content, AnonymousServerSession session)
        {
            var cancellationToken = LifetimeCancellationSource.Token;
            try
            {
                var service = GetStatsService(session);
                if (operation == AnonymousNet.OperationEnum.ResetStats)
                {
                    await service.ResetAsync(cancellationToken);
                    return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Success);
                }

                var argument = await DeserializeAsync<StatsRequestArgument>(content);
                if (argument == null)
                    return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

                switch (operation)
                {
                    case AnonymousNet.OperationEnum.EnsureStats:
                        if (argument.DefinitionsJson != null)
                            await service.EnsureAsync(argument.DefinitionsJson, cancellationToken);
                        else if (argument.Type == StatsTypeEnum.Int)
                            await service.EnsureAsync(argument.Key, argument.IntValue, cancellationToken);
                        else if (argument.Type == StatsTypeEnum.Float)
                            await service.EnsureAsync(argument.Key, argument.FloatValue, cancellationToken);
                        else if (argument.Type == StatsTypeEnum.AverageRate)
                            await service.EnsureAverageRateAsync(argument.Key, argument.FloatValue, argument.AverageRateWindowSeconds, cancellationToken);
                        else
                            return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

                        break;
                    case AnonymousNet.OperationEnum.GetStatDefinition:
                        return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, new StatsResponseArgument() { DefinitionsJson = new MyProgressDefinitions() { Stats = new[] { await service.GetDefinitionAsync(argument.Key, argument.Language, cancellationToken) } }.ToJson() });
                    case AnonymousNet.OperationEnum.GetStatDefinitions:
                        return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, new StatsResponseArgument() { DefinitionsJson = new MyProgressDefinitions() { Stats = await service.GetDefinitionsAsync(argument.Language, cancellationToken) }.ToJson() });
                    case AnonymousNet.OperationEnum.GetStatValues:
                        return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, new StatsResponseArgument() { Values = (await service.GetValuesAsync(cancellationToken)).Select(entry => (entry.Key, entry.Value)).ToArray() });

                    case AnonymousNet.OperationEnum.GetGlobalFloatStat:
                        return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, new StatsResponseArgument() { GlobalFloatValue = await service.GetGlobalFloatAsync(argument.Key, cancellationToken) });
                    case AnonymousNet.OperationEnum.GetGlobalIntStat:
                        return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, new StatsResponseArgument() { GlobalIntValue = await service.GetGlobalIntAsync(argument.Key, cancellationToken) });
                    case AnonymousNet.OperationEnum.AddStats:
                        if (argument.Type == StatsTypeEnum.Int)
                            await service.AddAsync(argument.Key, argument.IntValue, cancellationToken);
                        else if (argument.Type == StatsTypeEnum.Float)
                            await service.AddAsync(argument.Key, argument.FloatValue, cancellationToken);
                        else
                            return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

                        break;
                    case AnonymousNet.OperationEnum.GetAverageRateStat:
                        return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, new StatsResponseArgument() { FloatValue = await service.GetAverageRateAsync(argument.Key, cancellationToken) });
                    case AnonymousNet.OperationEnum.GetFloatStat:
                        return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, new StatsResponseArgument() { FloatValue = await service.GetFloatAsync(argument.Key, cancellationToken) });
                    case AnonymousNet.OperationEnum.GetIntStat:
                        return await AnonymousServerResponse.CreateAsync(AnonymousServerResponse.ResultCodeEnum.Success, new StatsResponseArgument() { IntValue = await service.GetIntAsync(argument.Key, cancellationToken) });
                    case AnonymousNet.OperationEnum.ResetStat:
                        await service.ResetAsync(argument.Key, cancellationToken);
                        break;
                    case AnonymousNet.OperationEnum.SetStats:
                        if (argument.Type == StatsTypeEnum.Int)
                            await service.SetAsync(argument.Key, argument.IntValue, cancellationToken);
                        else if (argument.Type == StatsTypeEnum.Float)
                            await service.SetAsync(argument.Key, argument.FloatValue, cancellationToken);
                        else
                            return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

                        break;
                    case AnonymousNet.OperationEnum.UpdateAverageRateStat:
                        await service.UpdateAverageRateAsync(argument.Key, argument.FloatValue, argument.AverageRateSessionLengthSeconds, cancellationToken);
                        break;
                    default:
                        return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.UnsupportedOperation);
                }

                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Success);
            }
            catch (UnauthorizedAccessException)
            {
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);
            }
            catch (Exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.ServerError);
            }
        }

        private async Task<AnonymousServerResponse> ResetAllProgressAsync(AnonymousServerSession session)
        {
            var cancellationToken = LifetimeCancellationSource.Token;
            try
            {
                await MyAnonymousProgressServer.GetServices(session.AppId, session.Account, MyProgressDefinitions.WriterEnum.Client).ResetAllAsync(cancellationToken);
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Success);
            }
            catch (UnauthorizedAccessException)
            {
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);
            }
            catch (Exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.ServerError);
            }
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
                var friendIds = new HashSet<string>(StringComparer.Ordinal);
                var friends = new List<FriendData>();
                foreach (var friendId in accounts)
                {
                    if (friendIds.Add(friendId) == false)
                        continue;

                    var friend = new FriendData()
                    {
                        Id = friendId,
                        Nickname = friendId,
                        RoomId = string.Empty,
                        State = MyNetFriendInterface.StateEnum.Offline,
                    };
                    if (TryGetFriendSession(session, friendId, out var friendSession))
                    {
                        friend.State = MyNetFriendInterface.StateEnum.Online;
                        var room = RoomState.Rooms.Find(value => (value.Room.Visibility != MyNetRoomInterface.VisibilityEnum.Private) && value.Room.Players.Any(player => player.Id == friendSession.Account));
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

        private async Task<AnonymousServerResponse> InviteFriendAsync(byte[] content, AnonymousServerSession session)
        {
            try
            {
                var argument = await DeserializeAsync<InviteFriendRequestArgument>(content);
                if ((argument == null) || string.IsNullOrWhiteSpace(argument.PlayerId) || string.IsNullOrWhiteSpace(argument.RoomId))
                    return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

                var room = RoomState.Rooms.Find(value => value.Room.Id == argument.RoomId);
                if (room == null)
                    return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.NotFound);

                if ((room.Room.Players ?? Array.Empty<AnonymousServerRoom.PlayerData>()).Any(player => player.Id == session.Account) == false)
                    return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

                if (TryGetFriendSession(session, argument.PlayerId, out var targetSession) == false)
                    return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Forbidden);

                targetSession.Messages.Send(AnonymousTransport.Message.CreateFriendInvited(MyNetSerializer.Serialize(new FriendInviteData() { PlayerId = session.Account, RoomId = argument.RoomId })));
                return AnonymousServerResponse.Create(AnonymousServerResponse.ResultCodeEnum.Success);
            }
            catch (Exception)
            {
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
            _isStarted = false;
        }

        internal void Start(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_isStarted)
                return;

            try
            {
                Listener.Start();
            }
            catch (SocketException exception) when (exception.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                return;
            }

            _isStarted = true;
            _ = AcceptAsync();
        }

        private bool TryGetFriendSession(AnonymousServerSession session, string playerId, out AnonymousServerSession friendSession)
        {
            return Sessions.TryGetValue(playerId, out friendSession) && (friendSession.AppId == session.AppId);
        }
    }
}
