#if STEAMWORKS_NET
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace oojjrs.oplat.steam
{
    internal sealed class SteamNet : MyNetInterface
    {
        private enum MessageKind : byte
        {
            AdmissionRequest = 1,
            AdmissionAccepted = 2,
            AdmissionRejected = 3,
            RosterChanged = 4,
            PlayerDataChanged = 5,
            Request = 6,
            Response = 7,
            RoomClosed = 8,
            PlayerKicked = 9,
            PlayerDataAccepted = 10,
            PlayerDataRejected = 11,
            PlayerUpdated = 12,
        }

        private enum StateEnum
        {
            Created,
            Ready,
            Creating,
            Joining,
            AwaitingAdmission,
            Host,
            Member,
            Leaving,
            Disposed,
        }

        private enum PlayerUpdateOutcomeEnum
        {
            Accepted,
            Rejected,
            Unknown,
        }

        private static class SteamPayloadDeserializer
        {
            private sealed class BoundedReadState
            {
                private readonly int MaxArrayLength;
                private readonly int MaxDepth;
                private readonly int MaxElementCount;
                private readonly int MaxObjectCount;
                private readonly int MaxStringBytes;

                private int _elementCount;
                private int _objectCount;
                private int _stringBytes;

                internal BoundedReadState(int maxArrayLength, int maxDepth, int maxElementCount, int maxObjectCount, int maxStringBytes)
                {
                    MaxArrayLength = maxArrayLength;
                    MaxDepth = maxDepth;
                    MaxElementCount = maxElementCount;
                    MaxObjectCount = maxObjectCount;
                    MaxStringBytes = maxStringBytes;
                }

                internal void AddArray(int length)
                {
                    if (length < 0)
                        throw new FormatException("Array length cannot be negative.");

                    if (length > MaxArrayLength)
                        throw new FormatException($"Array length {length} exceeds the configured limit {MaxArrayLength}.");

                    if (length > (MaxElementCount - _elementCount))
                        throw new FormatException($"Array elements exceed the configured total limit {MaxElementCount}.");

                    _elementCount += length;
                    AddObject();
                }

                internal void AddObject()
                {
                    if (_objectCount >= MaxObjectCount)
                        throw new FormatException($"Objects exceed the configured total limit {MaxObjectCount}.");

                    ++_objectCount;
                }

                internal void AddStringBytes(int byteCount)
                {
                    if (byteCount < 0)
                        throw new FormatException("String byte length cannot be negative.");

                    if (byteCount > (MaxStringBytes - _stringBytes))
                        throw new FormatException($"String data exceeds the configured total byte limit {MaxStringBytes}.");

                    _stringBytes += byteCount;
                }

                internal void EnsureDepth(int depth)
                {
                    if (depth > MaxDepth)
                        throw new FormatException($"Object depth {depth} exceeds the configured limit {MaxDepth}.");
                }
            }

            private static readonly Dictionary<Type, PropertyInfo[]> PropertyCache = new();
            private static readonly UTF8Encoding StringEncoding = new(false, true);

            internal static T Deserialize<T>(Stream stream, int maxArrayLength, int maxDepth, int maxElementCount, int maxObjectCount, int maxStringBytes) where T : class
            {
                if (stream is null)
                    throw new ArgumentNullException(nameof(stream));

                if (stream.CanRead == false)
                    throw new ArgumentException("The stream must be readable.", nameof(stream));

                if (stream.CanSeek == false)
                    throw new ArgumentException("The bounded deserializer requires a seekable stream.", nameof(stream));

                if (maxArrayLength < 0)
                    throw new ArgumentOutOfRangeException(nameof(maxArrayLength));

                if (maxDepth <= 0)
                    throw new ArgumentOutOfRangeException(nameof(maxDepth));

                if (maxElementCount < 0)
                    throw new ArgumentOutOfRangeException(nameof(maxElementCount));

                if (maxObjectCount <= 0)
                    throw new ArgumentOutOfRangeException(nameof(maxObjectCount));

                if (maxStringBytes <= 0)
                    throw new ArgumentOutOfRangeException(nameof(maxStringBytes));

                var state = new BoundedReadState(maxArrayLength, maxDepth, maxElementCount, maxObjectCount, maxStringBytes);
                var reader = new BinaryReader(stream);
                var name = ReadBoundedString(reader, state);
                if (string.IsNullOrWhiteSpace(name))
                    throw new FormatException("The serialized type name is empty.");

                var type = GetLoadedType(name);
                if (type is null)
                    throw new FormatException($"The serialized type '{name}' is not available in an already loaded assembly.");

                if (typeof(T).IsAssignableFrom(type) == false)
                    throw new FormatException($"The serialized type '{type.FullName}' is not assignable to {typeof(T).FullName}.");

                if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
                    throw new FormatException($"The serialized type '{type.FullName}' must be concrete and closed.");

                var value = ReadBoundedClass(reader, type, GetProperties(type), state, 1);
                if (stream.Position != stream.Length)
                    throw new FormatException("The serialized payload contains trailing data.");

                return (T)value;
            }

            private static PropertyInfo[] GetProperties(Type type)
            {
                if (PropertyCache.TryGetValue(type, out var value) == false)
                {
                    value = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.SetProperty).Where(t => t.CanRead && t.CanWrite).OrderBy(t => t.Name).ToArray();
                    PropertyCache[type] = value;
                }

                return value;
            }

            private static Type GetLoadedType(string name)
            {
                try
                {
                    return Type.GetType(name, ResolveLoadedAssembly, ResolveTypeFromLoadedAssembly, false);
                }
                catch (Exception exception) when ((exception is ArgumentException) || (exception is BadImageFormatException) || (exception is FileLoadException) || (exception is FileNotFoundException) || (exception is TargetInvocationException) || (exception is TypeLoadException))
                {
                    throw new FormatException($"The serialized type name '{name}' is invalid.", exception);
                }
            }

            private static bool IsItem(Type propertyType)
            {
                return propertyType.IsPrimitive || (propertyType == typeof(string)) || (propertyType == typeof(DateTime));
            }

            private static bool IsTuple(Type type)
            {
                if (type.IsGenericType == false)
                    return false;

                var openType = type.GetGenericTypeDefinition();
                return openType == typeof(ValueTuple<>)
                    || openType == typeof(ValueTuple<,>)
                    || openType == typeof(ValueTuple<,,>)
                    || openType == typeof(ValueTuple<,,,>)
                    || openType == typeof(ValueTuple<,,,,>)
                    || openType == typeof(ValueTuple<,,,,,>)
                    || openType == typeof(ValueTuple<,,,,,,>)
                    || (openType == typeof(ValueTuple<,,,,,,,>) && IsTuple(type.GetGenericArguments()[7]));
            }

            private static object ReadBoundedClass(BinaryReader reader, Type type, PropertyInfo[] properties, BoundedReadState state, int depth)
            {
                state.EnsureDepth(depth);
                state.AddObject();
                var value = Activator.CreateInstance(type);
                foreach (var property in properties)
                {
                    var exists = reader.ReadBoolean();
                    if (exists == false)
                        continue;

                    if (property.PropertyType.IsArray)
                    {
                        var elementType = property.PropertyType.GetElementType();
                        var length = reader.ReadInt32();
                        state.AddArray(length);
                        var array = Array.CreateInstance(elementType, length);
                        if (elementType.IsEnum)
                        {
                            for (var index = 0; index < length; ++index)
                                array.SetValue(ReadBoundedEnum(reader, elementType, state), index);
                        }
                        else if (IsItem(elementType))
                        {
                            for (var index = 0; index < length; ++index)
                                array.SetValue(ReadBoundedItem(reader, elementType, state), index);
                        }
                        else if (IsTuple(elementType))
                        {
                            for (var index = 0; index < length; ++index)
                                array.SetValue(ReadBoundedTuple(reader, elementType, state, depth + 1), index);
                        }
                        else
                        {
                            var elementProperties = GetProperties(elementType);
                            for (var index = 0; index < length; ++index)
                                array.SetValue(ReadBoundedClass(reader, elementType, elementProperties, state, depth + 1), index);
                        }

                        property.SetValue(value, array);
                    }
                    else if (property.PropertyType.IsEnum)
                    {
                        property.SetValue(value, ReadBoundedEnum(reader, property.PropertyType, state));
                    }
                    else if (IsItem(property.PropertyType))
                    {
                        property.SetValue(value, ReadBoundedItem(reader, property.PropertyType, state));
                    }
                    else if (IsTuple(property.PropertyType))
                    {
                        property.SetValue(value, ReadBoundedTuple(reader, property.PropertyType, state, depth + 1));
                    }
                    else
                    {
                        property.SetValue(value, ReadBoundedClass(reader, property.PropertyType, GetProperties(property.PropertyType), state, depth + 1));
                    }
                }

                return value;
            }

            private static object ReadBoundedEnum(BinaryReader reader, Type enumType, BoundedReadState state)
            {
                var value = ReadBoundedString(reader, state);
                try
                {
                    return Enum.Parse(enumType, value);
                }
                catch (ArgumentException exception)
                {
                    throw new FormatException($"The value '{value}' is invalid for enum {enumType.FullName}.", exception);
                }
            }

            private static int ReadBounded7BitEncodedInt(BinaryReader reader)
            {
                var result = 0u;
                for (var shift = 0; shift < 35; shift += 7)
                {
                    var value = reader.ReadByte();
                    if ((shift == 28) && ((value & 0xf0) != 0))
                        throw new FormatException("The serialized string length is invalid.");

                    result |= (uint)(value & 0x7f) << shift;
                    if ((value & 0x80) == 0)
                    {
                        if (result > int.MaxValue)
                            throw new FormatException("The serialized string length is invalid.");

                        return (int)result;
                    }
                }

                throw new FormatException("The serialized string length is invalid.");
            }

            private static object ReadBoundedItem(BinaryReader reader, Type type, BoundedReadState state)
            {
                if (type == typeof(string))
                    return ReadBoundedString(reader, state);

                if (type == typeof(float))
                    return reader.ReadSingle();
                if (type == typeof(long))
                    return reader.ReadInt64();
                if (type == typeof(int))
                    return reader.ReadInt32();
                if (type == typeof(short))
                    return reader.ReadInt16();
                if (type == typeof(byte))
                    return reader.ReadByte();
                if (type == typeof(bool))
                    return reader.ReadBoolean();
                if (type == typeof(double))
                    return reader.ReadDouble();
                if (type == typeof(char))
                    return reader.ReadChar();
                if (type == typeof(DateTime))
                    return DateTime.FromBinary(reader.ReadInt64());

                throw new NotImplementedException();
            }

            private static string ReadBoundedString(BinaryReader reader, BoundedReadState state)
            {
                var byteCount = ReadBounded7BitEncodedInt(reader);
                state.AddStringBytes(byteCount);
                if (byteCount > (reader.BaseStream.Length - reader.BaseStream.Position))
                    throw new EndOfStreamException("The serialized string is truncated.");

                var bytes = reader.ReadBytes(byteCount);
                if (bytes.Length != byteCount)
                    throw new EndOfStreamException("The serialized string is truncated.");

                try
                {
                    return StringEncoding.GetString(bytes);
                }
                catch (DecoderFallbackException exception)
                {
                    throw new FormatException("The serialized string contains invalid UTF-8 data.", exception);
                }
            }

            private static object ReadBoundedTuple(BinaryReader reader, Type type, BoundedReadState state, int depth)
            {
                state.EnsureDepth(depth);
                state.AddObject();
                var value = Activator.CreateInstance(type);
                foreach (var field in type.GetFields())
                {
                    if (field.FieldType.IsEnum)
                        field.SetValue(value, ReadBoundedEnum(reader, field.FieldType, state));
                    else if (IsItem(field.FieldType))
                        field.SetValue(value, ReadBoundedItem(reader, field.FieldType, state));
                    else if (IsTuple(field.FieldType))
                        field.SetValue(value, ReadBoundedTuple(reader, field.FieldType, state, depth + 1));
                    else
                        field.SetValue(value, ReadBoundedClass(reader, field.FieldType, GetProperties(field.FieldType), state, depth + 1));
                }

                return value;
            }

            private static Assembly ResolveLoadedAssembly(AssemblyName name)
            {
                return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => string.Equals(assembly.FullName, name.FullName, StringComparison.Ordinal));
            }

            private static Type ResolveTypeFromLoadedAssembly(Assembly assembly, string name, bool ignoreCase)
            {
                if (assembly is null)
                    return null;

                return assembly.GetType(name, false, ignoreCase);
            }
        }

        private sealed class BusyException : Exception
        {
        }

        private sealed class FailureException : Exception
        {
            internal readonly MyNetInterface.CatchInterface.FailureEnum Failure;

            internal FailureException(MyNetInterface.CatchInterface.FailureEnum failure)
            {
                Failure = failure;
            }
        }

        private sealed class RosterPlayerData
        {
            internal MyNetInterface.Field[] Fields;
            internal ulong Id;
            internal string Nickname;
        }

        private readonly struct CallOutcome<T>
        {
            internal readonly bool IOFailure;
            internal readonly T Value;

            internal CallOutcome(T value, bool ioFailure)
            {
                Value = value;
                IOFailure = ioFailure;
            }
        }

        private const int AdmissionTimeoutMilliseconds = 15000;
        private const int FieldCountMax = 128;
        private const int FieldKeyByteCountMax = 256;
        private const int FieldValueByteCountMax = 4096;
        private const int LobbySearchResultCountMax = 50;
        private const int MessageByteCountMax = 64 * 1024;
        private const int MessageChannel = 45831;
        private const int MessageQueueCountMax = 256;
        private const int MessagesPerFrameMax = 32;
        private const int MetadataValueByteCountMax = Constants.k_cubChatMetadataMax - 1;
        private const int MinimumPollingDelaySeconds = 1;
        private const int PlayerCountMax = 250;
        private const int ProtocolVersion = 1;
        private const int RosterChunkCharacterCount = 7000;
        private const int RosterChunkCountMax = 16;
        private const uint ChatMagic = 0x4f504c48;
        private const uint ControlMagic = 0x4f504c43;
        private const uint MessageMagic = 0x4f504c4e;
        private const string CodeAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        private const string MetadataClosed = "oplat.closed";
        private const string MetadataEpoch = "oplat.epoch";
        private const string MetadataHasPassword = "oplat.password";
        private const string MetadataHost = "oplat.host";
        private const string MetadataIsLocked = "oplat.locked";
        private const string MetadataIsPrivate = "oplat.private";
        private const string MetadataMaxPlayers = "oplat.maxPlayers";
        private const string MetadataPlayerFields = "oplat.player.fields";
        private const string MetadataPlayerNickname = "oplat.player.nickname";
        private const string MetadataRoomFields = "oplat.room.fields";
        private const string MetadataRosterChunkCount = "oplat.roster.count";
        private const string MetadataRosterChunkPrefix = "oplat.roster.";
        private const string MetadataRosterRevision = "oplat.roster.revision";
        private const string MetadataSchema = "oplat.schema";
        private const string MetadataTitle = "oplat.title";

        private readonly HashSet<ulong> AcceptedPlayerIds = new();
        private readonly HashSet<ulong> BlockedPlayerIds = new();
        private readonly Queue<MyNetRequest> IncomingRequests = new();
        private readonly Queue<MyNetResponse> IncomingResponses = new();
        private readonly Dictionary<ulong, RosterPlayerData> LogicalPlayers = new();
        private readonly object OutgoingRequestLock = new();
        private readonly Queue<MyNetRequest> OutgoingRequests = new();
        private readonly object OutgoingResponseLock = new();
        private readonly Queue<MyNetResponse> OutgoingResponses = new();
        private readonly SemaphoreSlim OperationGate = new(1, 1);
        private readonly Dictionary<ulong, TaskCompletionSource<bool>> PendingLobbyData = new();
        private readonly HashSet<ulong> PendingRosterPlayerIds = new();

        private TaskCompletionSource<bool> _admissionSource;
        private MyNetChatResultInterface _chatResult;
        private string _chatRoomId;
        private CSteamID _currentLobby;
        private string _epoch;
        private MyNetHostResultInterface _hostResult;
        private bool _isInitialized;
        private bool _isLobbyPolling;
        private bool _isLocked;
        private bool _isPrivate;
        private CancellationTokenSource _lifetimeSource;
        private Callback<LobbyChatMsg_t> _lobbyChatMessageCallback;
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdateCallback;
        private Callback<LobbyDataUpdate_t> _lobbyDataUpdateCallback;
        private MyNetLobbyServiceInterface.ConfigInterface _lobbyPollingConfig;
        private int _lobbyPollingGeneration;
        private MyNetLobbyServiceInterface.ResultInterface _lobbyPollingResult;
        private MyNetInterface.Field[] _localPlayerFields = Array.Empty<MyNetInterface.Field>();
        private string _localPlayerNickname;
        private ulong _localSteamId;
        private int _mainThreadId;
        private int _maxPlayers;
        private MyNetMemberResultInterface _memberResult;
        private MyNetInterface.Field[] _memberRoomFields = Array.Empty<MyNetInterface.Field>();
        private Callback<SteamNetworkingMessagesSessionFailed_t> _messageSessionFailedCallback;
        private Callback<SteamNetworkingMessagesSessionRequest_t> _messageSessionRequestCallback;
        private float _nextLobbyPollTimeSeconds;
        private ulong _nextPlayerUpdateId;
        private ulong _originalHostId;
        private string _password;
        private ulong _pendingPlayerUpdateId;
        private MessageKind _pendingRosterKind;
        private byte[] _pendingRosterPayload;
        private MyNetPlayerServiceInterface.UpdateResultInterface _playerResult;
        private TaskCompletionSource<PlayerUpdateOutcomeEnum> _playerUpdateSource;
        private MyNetInterface.Field[] _roomFields = Array.Empty<MyNetInterface.Field>();
        private MyNetRoomServiceInterface.UpdateResultInterface _roomResult;
        private volatile StateEnum _state;
        private string _title;
        private bool _useLocal;

        internal SteamNet()
        {
            Chat = new SteamNetChatService(this);
            Host = new SteamNetHostService(this);
            Lobby = new SteamNetLobbyService(this);
            Member = new SteamNetMemberService(this);
            Player = new SteamNetPlayerService(this);
            Room = new SteamNetRoomService(this);
        }

        private MyNetChatServiceInterface Chat { get; }
        private MyNetHostServiceInterface Host { get; }
        private MyNetLobbyServiceInterface Lobby { get; }
        private MyNetMemberServiceInterface Member { get; }
        private MyNetPlayerServiceInterface Player { get; }
        private MyNetRoomServiceInterface Room { get; }

        MyNetChatServiceInterface MyNetInterface.Chat => Chat;
        MyNetHostServiceInterface MyNetInterface.Host => Host;
        MyNetLobbyServiceInterface MyNetInterface.Lobby => Lobby;
        MyNetMemberServiceInterface MyNetInterface.Member => Member;
        MyNetPlayerServiceInterface MyNetInterface.Player => Player;
        MyNetRoomServiceInterface MyNetInterface.Room => Room;
        bool MyNetInterface.UseLocal
        {
            get => _useLocal;
            set => _useLocal = value;
        }

        internal void Initialize(MyNetChatResultInterface chatResult, MyNetHostResultInterface hostResult, MyNetMemberResultInterface memberResult, MyNetPlayerServiceInterface.UpdateResultInterface playerResult, MyNetRoomServiceInterface.UpdateResultInterface roomResult)
        {
            if (_isInitialized)
                return;

            _chatResult = chatResult ?? throw new ArgumentNullException(nameof(chatResult));
            _hostResult = hostResult ?? throw new ArgumentNullException(nameof(hostResult));
            _memberResult = memberResult ?? throw new ArgumentNullException(nameof(memberResult));
            _playerResult = playerResult ?? throw new ArgumentNullException(nameof(playerResult));
            _roomResult = roomResult ?? throw new ArgumentNullException(nameof(roomResult));
            _mainThreadId = Environment.CurrentManagedThreadId;
            var localSteamId = SteamUser.GetSteamID();
            if ((localSteamId.IsValid() == false) || (localSteamId.BIndividualAccount() == false))
                throw new InvalidOperationException("Steam returned an invalid local user ID.");

            _localSteamId = localSteamId.m_SteamID;
            _lifetimeSource = new CancellationTokenSource();
            try
            {
                _lobbyChatMessageCallback = Callback<LobbyChatMsg_t>.Create(OnLobbyChatMessage);
                _lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
                _lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
                _messageSessionFailedCallback = Callback<SteamNetworkingMessagesSessionFailed_t>.Create(OnMessageSessionFailed);
                _messageSessionRequestCallback = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnMessageSessionRequest);
                _state = StateEnum.Ready;
                _isInitialized = true;
            }
            catch
            {
                _lobbyChatMessageCallback?.Dispose();
                _lobbyChatUpdateCallback?.Dispose();
                _lobbyDataUpdateCallback?.Dispose();
                _messageSessionFailedCallback?.Dispose();
                _messageSessionRequestCallback?.Dispose();
                _lifetimeSource.Dispose();
                _lifetimeSource = null;
                throw;
            }
        }

        internal void Shutdown()
        {
            if (_isInitialized == false)
                return;

            EnsureMainThread();
            _state = StateEnum.Disposed;
            _isInitialized = false;
            ++_lobbyPollingGeneration;
            _lobbyPollingConfig = null;
            _lobbyPollingResult = null;
            _lifetimeSource.Cancel();
            ResetSession(true, StateEnum.Disposed);

            foreach (var source in PendingLobbyData.Values)
                source.TrySetCanceled();

            PendingLobbyData.Clear();
            DisposeSafely(_lobbyChatMessageCallback);
            DisposeSafely(_lobbyChatUpdateCallback);
            DisposeSafely(_lobbyDataUpdateCallback);
            DisposeSafely(_messageSessionFailedCallback);
            DisposeSafely(_messageSessionRequestCallback);
            _lobbyChatMessageCallback = null;
            _lobbyChatUpdateCallback = null;
            _lobbyDataUpdateCallback = null;
            _messageSessionFailedCallback = null;
            _messageSessionRequestCallback = null;
            _lifetimeSource.Dispose();
            _lifetimeSource = null;
        }

        private static void DisposeSafely(IDisposable disposable)
        {
            if (disposable == null)
                return;

            try
            {
                disposable.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to dispose a Steam callback: {exception.Message}");
            }
        }

        internal void Update()
        {
            if (_isInitialized == false)
                return;

            EnsureMainThread();
            ReceiveMessages();
            FlushPendingRoster();
            FlushResponses();
            HandleResponses();
            FlushRequests();
            HandleRequests();
            UpdateLobbyPolling();

            if ((_currentLobby.m_SteamID != 0) && (SteamMatchmaking.GetLobbyOwner(_currentLobby).m_SteamID != _originalHostId))
                ResetSession(true, StateEnum.Ready);
        }

        internal async Task RefreshLobbyAsync(MyNetLobbyServiceInterface.ResultInterface result, CancellationToken callerCancellationToken)
        {
            EnsureInitialized();
            using (var cancellationSource = CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                if (await OperationGate.WaitAsync(0, cancellationToken) == false)
                {
                    result.OnBusy();
                    return;
                }

                MyNetRoomInterface[] rooms = null;
                var isBusy = false;
                Exception caughtException = null;
                try
                {
                    rooms = await RefreshLobbyCoreAsync(cancellationToken);
                }
                catch (BusyException)
                {
                    isBusy = true;
                }
                catch (Exception exception)
                {
                    caughtException = exception;
                }
                finally
                {
                    OperationGate.Release();
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (isBusy)
                    result.OnBusy();
                else if (caughtException != null)
                    result.OnException(new MyNetSessionException("Failed to get Steam rooms.", caughtException));
                else
                    result.OnOk(rooms);
            }
        }

        internal Task StartLobbyAsync(MyNetLobbyServiceInterface.ConfigInterface config, MyNetLobbyServiceInterface.ResultInterface result)
        {
            EnsureInitialized();
            ++_lobbyPollingGeneration;
            _lobbyPollingConfig = config;
            _lobbyPollingResult = result;
            _nextLobbyPollTimeSeconds = Time.realtimeSinceStartup + Math.Max(MinimumPollingDelaySeconds, config.PollingDelaySeconds);
            return RefreshLobbyAsync(result, config.CancellationToken);
        }

        internal void StopLobby()
        {
            EnsureInitialized();
            ++_lobbyPollingGeneration;
            _lobbyPollingConfig = null;
            _lobbyPollingResult = null;
        }

        internal async Task ExitChatAsync(MyNetChatServiceInterface.ExitConfigInterface config, MyNetChatServiceInterface.ExitResultInterface result)
        {
            EnsureInitialized();
            var roomId = config.RoomId;
            if (string.IsNullOrWhiteSpace(roomId))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                return;
            }

            using (var cancellationSource = CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                if (_useLocal)
                {
                    result.OnOk(roomId);
                    return;
                }

                if (await OperationGate.WaitAsync(0, cancellationToken) == false)
                {
                    result.OnBusy();
                    return;
                }

                MyNetInterface.CatchInterface.FailureEnum? failure = null;
                Exception caughtException = null;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    EnsureCurrentRoom(roomId);
                    if (_chatRoomId != roomId)
                        throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);

                    _chatRoomId = null;
                }
                catch (FailureException exception)
                {
                    failure = exception.Failure;
                }
                catch (Exception exception)
                {
                    caughtException = exception;
                }
                finally
                {
                    OperationGate.Release();
                }

                if (failure.HasValue || (caughtException != null))
                    cancellationToken.ThrowIfCancellationRequested();

                if (failure.HasValue)
                    result.OnFailed(failure.Value);
                else if (caughtException != null)
                    result.OnException(new MyNetSessionException("Failed to exit Steam chat.", caughtException));
                else
                    result.OnOk(roomId);
            }
        }

        internal async Task JoinChatAsync(MyNetChatServiceInterface.JoinConfigInterface config, MyNetChatServiceInterface.JoinResultInterface result)
        {
            EnsureInitialized();
            var roomId = config.RoomId;
            if (string.IsNullOrWhiteSpace(roomId))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                return;
            }

            using (var cancellationSource = CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                if (_useLocal)
                {
                    result.OnOk(roomId);
                    return;
                }

                if (await OperationGate.WaitAsync(0, cancellationToken) == false)
                {
                    result.OnBusy();
                    return;
                }

                MyNetInterface.CatchInterface.FailureEnum? failure = null;
                Exception caughtException = null;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    EnsureCurrentRoom(roomId);
                    if (AcceptedPlayerIds.Contains(_localSteamId) == false)
                        throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotPermitted);

                    _chatRoomId = roomId;
                }
                catch (FailureException exception)
                {
                    failure = exception.Failure;
                }
                catch (Exception exception)
                {
                    caughtException = exception;
                }
                finally
                {
                    OperationGate.Release();
                }

                if (failure.HasValue || (caughtException != null))
                    cancellationToken.ThrowIfCancellationRequested();

                if (failure.HasValue)
                    result.OnFailed(failure.Value);
                else if (caughtException != null)
                    result.OnException(new MyNetSessionException("Failed to join Steam chat.", caughtException));
                else
                    result.OnOk(roomId);
            }
        }

        internal async Task SendChatAsync(MyNetChatServiceInterface.SendConfigInterface config, MyNetChatServiceInterface.SendResultInterface result)
        {
            EnsureInitialized();
            var message = config.Message;
            var roomId = config.RoomId;
            if (string.IsNullOrWhiteSpace(roomId))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyMessage);
                return;
            }

            if (Encoding.UTF8.GetByteCount(message) > SteamNetChatService.MessageByteCountMax)
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.MessageTooLong);
                return;
            }

            using (var cancellationSource = CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                if (_useLocal)
                {
                    _chatResult.OnReceived(message, _localSteamId.ToString(), roomId);
                    result.OnOk(roomId);
                    return;
                }

                if (await OperationGate.WaitAsync(0, cancellationToken) == false)
                {
                    result.OnBusy();
                    return;
                }

                MyNetInterface.CatchInterface.FailureEnum? failure = null;
                Exception caughtException = null;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    EnsureCurrentRoom(roomId);
                    if ((_chatRoomId != roomId) || (AcceptedPlayerIds.Contains(_localSteamId) == false))
                        throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotPermitted);

                    var data = EncodeChat(message);
                    if (SteamMatchmaking.SendLobbyChatMsg(_currentLobby, data, data.Length) == false)
                        throw new InvalidOperationException("Steam rejected the chat message.");
                }
                catch (FailureException exception)
                {
                    failure = exception.Failure;
                }
                catch (Exception exception)
                {
                    caughtException = exception;
                }
                finally
                {
                    OperationGate.Release();
                }

                if (failure.HasValue || (caughtException != null))
                    cancellationToken.ThrowIfCancellationRequested();

                if (failure.HasValue)
                    result.OnFailed(failure.Value);
                else if (caughtException != null)
                    result.OnException(new MyNetSessionException("Failed to send Steam chat message.", caughtException));
                else
                    result.OnOk(roomId);
            }
        }

        internal async Task CreateRoomAsync(MyNetRoomServiceInterface.CreateConfigInterface config, MyNetRoomServiceInterface.CreateResultInterface result)
        {
            EnsureInitialized();
            using (var cancellationSource = CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                if (await OperationGate.WaitAsync(0, cancellationToken) == false)
                {
                    result.OnBusy();
                    return;
                }

                MyNetRoomInterface room = null;
                var isBusy = false;
                MyNetInterface.CatchInterface.FailureEnum? failure = null;
                Exception caughtException = null;
                try
                {
                    room = await CreateRoomCoreAsync(config, cancellationToken);
                }
                catch (BusyException)
                {
                    isBusy = true;
                }
                catch (FailureException exception)
                {
                    failure = exception.Failure;
                }
                catch (Exception exception)
                {
                    caughtException = exception;
                }
                finally
                {
                    OperationGate.Release();
                }

                if (room == null)
                    cancellationToken.ThrowIfCancellationRequested();

                if (isBusy)
                    result.OnBusy();
                else if (failure.HasValue)
                    result.OnFailed(failure.Value);
                else if (caughtException != null)
                    result.OnException(new MyNetSessionException("Failed to create Steam room.", caughtException));
                else
                    result.OnOk(room);
            }
        }

        internal async Task ExitRoomAsync(MyNetRoomServiceInterface.ExitConfigInterface config, MyNetRoomServiceInterface.ExitResultInterface result)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(config.PlayerId))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyPlayerId);
                return;
            }

            if (string.IsNullOrWhiteSpace(config.RoomId))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                return;
            }

            using (var cancellationSource = CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                if (await OperationGate.WaitAsync(0, cancellationToken) == false)
                {
                    result.OnBusy();
                    return;
                }

                MyNetInterface.CatchInterface.FailureEnum? failure = null;
                Exception caughtException = null;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ExitRoomCore(config.RoomId, config.PlayerId);
                }
                catch (FailureException exception)
                {
                    failure = exception.Failure;
                }
                catch (Exception exception)
                {
                    caughtException = exception;
                }
                finally
                {
                    OperationGate.Release();
                }

                if (failure.HasValue || (caughtException != null))
                    cancellationToken.ThrowIfCancellationRequested();

                if (failure.HasValue)
                    result.OnFailed(failure.Value);
                else if (caughtException != null)
                    result.OnException(new MyNetSessionException("Failed to exit Steam room.", caughtException));
                else
                    result.OnOk(config.RoomId, config.PlayerId);
            }
        }

        internal async Task JoinRoomAsync(MyNetRoomServiceInterface.JoinConfigInterface config, MyNetRoomServiceInterface.JoinResultInterface result)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(config.RoomId) && string.IsNullOrWhiteSpace(config.Code))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                return;
            }

            using (var cancellationSource = CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                if (await OperationGate.WaitAsync(0, cancellationToken) == false)
                {
                    result.OnBusy();
                    return;
                }

                MyNetRoomInterface room = null;
                var isBusy = false;
                MyNetInterface.CatchInterface.FailureEnum? failure = null;
                Exception caughtException = null;
                try
                {
                    room = await JoinRoomCoreAsync(config, cancellationToken);
                }
                catch (BusyException)
                {
                    isBusy = true;
                }
                catch (FailureException exception)
                {
                    failure = exception.Failure;
                }
                catch (Exception exception)
                {
                    caughtException = exception;
                }
                finally
                {
                    OperationGate.Release();
                }

                if (room == null)
                    cancellationToken.ThrowIfCancellationRequested();

                if (isBusy)
                    result.OnBusy();
                else if (failure.HasValue)
                    result.OnFailed(failure.Value);
                else if (caughtException != null)
                    result.OnException(new MyNetSessionException("Failed to join Steam room.", caughtException));
                else
                    result.OnOk(room);
            }
        }

        internal async Task UpdateRoomAsync(MyNetRoomServiceInterface.UpdateConfigInterface config, MyNetRoomServiceInterface.UpdateResultInterface result)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(config.RoomId))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                return;
            }

            using (var cancellationSource = CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                if (await OperationGate.WaitAsync(0, cancellationToken) == false)
                {
                    result.OnBusy();
                    return;
                }

                MyNetRoomInterface room = null;
                MyNetInterface.CatchInterface.FailureEnum? failure = null;
                Exception caughtException = null;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    EnsureCurrentRoom(config.RoomId);
                    if (_state != StateEnum.Host)
                        throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotPermitted);

                    var previousRoomFields = _roomFields;
                    var previousIsPrivate = _isPrivate;
                    try
                    {
                        _roomFields = MergeFields(_roomFields, NormalizeFields(config.RoomFields));
                        _isPrivate = config.IsPrivate;
                        EncodeMemberSnapshot();
                        PublishRoomData();
                        PublishRoster();
                        BroadcastRoster();
                        room = BuildCurrentRoom();
                    }
                    catch
                    {
                        _roomFields = previousRoomFields;
                        _isPrivate = previousIsPrivate;
                        try
                        {
                            PublishRoomData();
                            PublishRoster();
                            BroadcastRoster();
                        }
                        catch (Exception cleanupException)
                        {
                            Debug.LogWarning($"Failed to republish Steam room rollback: {cleanupException.Message}");
                        }

                        throw;
                    }
                }
                catch (FailureException exception)
                {
                    failure = exception.Failure;
                }
                catch (Exception exception)
                {
                    caughtException = exception;
                }
                finally
                {
                    OperationGate.Release();
                }

                if (room == null)
                    cancellationToken.ThrowIfCancellationRequested();

                if (failure.HasValue)
                    result.OnFailed(failure.Value);
                else if (caughtException != null)
                    result.OnException(new MyNetSessionException("Failed to update Steam room.", caughtException));
                else
                    result.OnOk(room);
            }
        }

        internal async Task UpdatePlayerAsync(MyNetPlayerServiceInterface.UpdateConfigInterface config, MyNetPlayerServiceInterface.UpdateResultInterface result)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(config.PlayerId))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyPlayerId);
                return;
            }

            if (string.IsNullOrWhiteSpace(config.RoomId))
            {
                result.OnFailed(MyNetInterface.CatchInterface.FailureEnum.EmptyRoomId);
                return;
            }

            using (var cancellationSource = CreateCancellationSource(config.CancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                if (await OperationGate.WaitAsync(0, cancellationToken) == false)
                {
                    result.OnBusy();
                    return;
                }

                MyNetPlayerInterface player = null;
                MyNetInterface.CatchInterface.FailureEnum? failure = null;
                Exception caughtException = null;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    EnsureCurrentRoom(config.RoomId);
                    if (config.PlayerId != _localSteamId.ToString())
                        throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotPermitted);

                    if (AcceptedPlayerIds.Contains(_localSteamId) == false)
                        throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);

                    var previousPlayerFields = _localPlayerFields;
                    var playerLobbyId = _currentLobby.m_SteamID;
                    try
                    {
                        _localPlayerFields = MergeFields(_localPlayerFields, NormalizeFields(config.PlayerFields));
                        EncodeMemberSnapshot();
                        if (_state == StateEnum.Host)
                        {
                            PublishLocalPlayerData();
                            PublishRoster();
                            BroadcastRoster(MessageKind.PlayerUpdated, _localSteamId);
                        }
                        else
                        {
                            var updateId = unchecked(++_nextPlayerUpdateId);
                            if (updateId == 0)
                                updateId = unchecked(++_nextPlayerUpdateId);

                            var updateSource = new TaskCompletionSource<PlayerUpdateOutcomeEnum>(TaskCreationOptions.RunContinuationsAsynchronously);
                            _pendingPlayerUpdateId = updateId;
                            _playerUpdateSource = updateSource;
                            if (SendMessage(_originalHostId, MessageKind.PlayerDataChanged, EncodePlayerUpdate(updateId, _localPlayerNickname, SelectNonPrivateFields(_localPlayerFields))) == false)
                                throw new InvalidOperationException("Steam rejected the player data update.");

                            var updateOutcome = await WaitForPlayerUpdateAsync(updateSource.Task, _lifetimeSource.Token);
                            if (updateOutcome == PlayerUpdateOutcomeEnum.Rejected)
                                throw new InvalidOperationException("The Steam host rejected the player data update.");

                            if (updateOutcome == PlayerUpdateOutcomeEnum.Unknown)
                            {
                                ResetSession(true, StateEnum.Ready);
                                throw new InvalidOperationException("The Steam player update result is unknown, so the room was left to prevent divergent state.");
                            }

                            _playerUpdateSource = null;
                            _pendingPlayerUpdateId = 0;
                            PublishLocalPlayerData();
                        }

                        player = BuildCurrentRoom().Players.First(value => value.Id == config.PlayerId);
                    }
                    catch
                    {
                        _playerUpdateSource = null;
                        _pendingPlayerUpdateId = 0;
                        if (_currentLobby.m_SteamID == playerLobbyId)
                        {
                            _localPlayerFields = previousPlayerFields;
                            try
                            {
                                PublishLocalPlayerData();
                                if (_state == StateEnum.Host)
                                {
                                    PublishRoster();
                                    BroadcastRoster();
                                }
                            }
                            catch (Exception cleanupException)
                            {
                                Debug.LogWarning($"Failed to republish Steam player rollback: {cleanupException.Message}");
                            }
                        }

                        throw;
                    }
                }
                catch (FailureException exception)
                {
                    failure = exception.Failure;
                }
                catch (Exception exception)
                {
                    caughtException = exception;
                }
                finally
                {
                    OperationGate.Release();
                }

                if (player == null)
                    cancellationToken.ThrowIfCancellationRequested();

                if (failure.HasValue)
                    result.OnFailed(failure.Value);
                else if (caughtException != null)
                    result.OnException(new MyNetSessionException("Failed to update Steam player.", caughtException));
                else
                    result.OnOk(player);
            }
        }

        internal void QueueRequest(MyNetRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var state = _state;
            if ((_useLocal == false) && (state != StateEnum.Host) && (state != StateEnum.Member))
                return;

            lock (OutgoingRequestLock)
            {
                state = _state;
                if ((_useLocal == false) && (state != StateEnum.Host) && (state != StateEnum.Member))
                    return;

                if (OutgoingRequests.Count >= MessageQueueCountMax)
                    return;

                OutgoingRequests.Enqueue(request);
            }
        }

        internal void QueueResponse(MyNetResponse response)
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            if ((_useLocal == false) && (_state != StateEnum.Host))
                return;

            lock (OutgoingResponseLock)
            {
                if ((_useLocal == false) && (_state != StateEnum.Host))
                    return;

                if (OutgoingResponses.Count >= MessageQueueCountMax)
                    return;

                OutgoingResponses.Enqueue(response);
            }
        }

        private void EnsureInitialized()
        {
            if (_isInitialized == false)
                throw new InvalidOperationException("Steam networking is not initialized.");

            EnsureMainThread();
        }

        private void EnsureMainThread()
        {
            if (Environment.CurrentManagedThreadId != _mainThreadId)
                throw new InvalidOperationException("Steam networking operations must run on the Unity main thread.");
        }

        private CancellationTokenSource CreateCancellationSource(CancellationToken callerCancellationToken)
        {
            EnsureInitialized();
            return CancellationTokenSource.CreateLinkedTokenSource(callerCancellationToken, _lifetimeSource.Token);
        }

        private async Task<MyNetRoomInterface[]> RefreshLobbyCoreAsync(CancellationToken cancellationToken)
        {
            SteamMatchmaking.AddRequestLobbyListStringFilter(MetadataSchema, ProtocolVersion.ToString(), ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(MetadataIsPrivate, "0", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(MetadataClosed, "0", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(LobbySearchResultCountMax);
            var outcome = await WaitForCallAsync<LobbyMatchList_t>(SteamMatchmaking.RequestLobbyList(), cancellationToken);
            if (outcome.IOFailure)
                throw new InvalidOperationException("Steam failed to return the lobby list.");

            var roomCount = (int)Math.Min((long)outcome.Value.m_nLobbiesMatching, LobbySearchResultCountMax);
            var rooms = new List<MyNetRoomInterface>(roomCount);
            for (var index = 0; index < roomCount; ++index)
            {
                try
                {
                    var lobby = SteamMatchmaking.GetLobbyByIndex(index);
                    rooms.Add(BuildSearchRoom(lobby));
                }
                catch (Exception exception) when ((exception is FormatException) || (exception is IOException) || (exception is ArgumentException) || (exception is OverflowException))
                {
                }
            }

            return rooms.ToArray();
        }

        private async Task<MyNetRoomInterface> CreateRoomCoreAsync(MyNetRoomServiceInterface.CreateConfigInterface config, CancellationToken cancellationToken)
        {
            if (_currentLobby.m_SteamID != 0)
                throw new BusyException();

            if ((config.MaxPlayers <= 0) || (config.MaxPlayers > PlayerCountMax))
                throw new ArgumentOutOfRangeException(nameof(config.MaxPlayers), $"Steam rooms support between 1 and {PlayerCountMax} players.");

            var password = config.Password ?? string.Empty;
            var title = config.Title ?? string.Empty;
            EnsureStringByteCount(password, 1024, "room password");
            EnsureStringByteCount(title, MetadataValueByteCountMax, "room title");
            var roomFields = NormalizeFields(config.RoomFields);
            var playerFields = NormalizeFields(config.PlayerFields);
            var playerNickname = GetNickname(config.PlayerNickname, new CSteamID(_localSteamId));
            _state = StateEnum.Creating;
            var lobbyType = config.IsPrivate ? ELobbyType.k_ELobbyTypeInvisible : ELobbyType.k_ELobbyTypePublic;
            CSteamID lobby;
            try
            {
                var outcome = await WaitForCallAsync<LobbyCreated_t>(SteamMatchmaking.CreateLobby(lobbyType, config.MaxPlayers), _lifetimeSource.Token);
                if (outcome.IOFailure)
                    throw new InvalidOperationException("Steam failed while creating the lobby.");

                if (outcome.Value.m_eResult != EResult.k_EResultOK)
                    ThrowCreateFailure(outcome.Value.m_eResult);

                lobby = new CSteamID(outcome.Value.m_ulSteamIDLobby);
                if ((lobby.IsValid() == false) || (lobby.IsLobby() == false))
                    throw new InvalidOperationException("Steam returned an invalid created lobby ID.");

                if (cancellationToken.IsCancellationRequested)
                {
                    SteamMatchmaking.LeaveLobby(lobby);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch
            {
                _state = StateEnum.Ready;
                throw;
            }

            _currentLobby = lobby;
            _originalHostId = _localSteamId;
            _epoch = Guid.NewGuid().ToString("N");
            _password = password;
            _title = title;
            _isLocked = config.IsLocked;
            _isPrivate = config.IsPrivate;
            _maxPlayers = config.MaxPlayers;
            _roomFields = roomFields;
            _localPlayerFields = playerFields;
            _localPlayerNickname = playerNickname;
            AcceptedPlayerIds.Clear();
            AcceptedPlayerIds.Add(_localSteamId);
            BlockedPlayerIds.Clear();

            try
            {
                if (SteamMatchmaking.SetLobbyJoinable(_currentLobby, false) == false)
                    throw new InvalidOperationException("Steam rejected the temporary lobby joinable setting.");

                SetLobbyData(MetadataClosed, "1");
                PublishLocalPlayerData();
                PublishRoomData();
                PublishRoster();
                SetLobbyData(MetadataClosed, "0");
                if (SteamMatchmaking.SetLobbyJoinable(_currentLobby, _isLocked == false) == false)
                    throw new InvalidOperationException("Steam rejected the lobby joinable setting.");

                cancellationToken.ThrowIfCancellationRequested();
                _state = StateEnum.Host;
                return BuildCurrentRoom();
            }
            catch
            {
                ResetSession(true, StateEnum.Ready);
                throw;
            }
        }

        private async Task<MyNetRoomInterface> JoinRoomCoreAsync(MyNetRoomServiceInterface.JoinConfigInterface config, CancellationToken cancellationToken)
        {
            if (_currentLobby.m_SteamID != 0)
            {
                var requestedLobby = ParseLobby(config.RoomId, config.Code);
                if ((_currentLobby == requestedLobby) && AcceptedPlayerIds.Contains(_localSteamId))
                    return BuildCurrentRoom();

                throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotPermitted);
            }

            var lobby = ParseLobby(config.RoomId, config.Code);
            var playerFields = NormalizeFields(config.PlayerFields);
            var playerNickname = GetNickname(config.PlayerNickname, new CSteamID(_localSteamId));
            var admissionPayload = EncodeAdmissionRequest(config.Password ?? string.Empty, playerNickname, SelectNonPrivateFields(playerFields));
            _state = StateEnum.Joining;
            try
            {
                if (await RequestLobbyDataAsync(lobby, cancellationToken) == false)
                    throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);

                ValidateLobbyForJoin(lobby);
                var outcome = await WaitForCallAsync<LobbyEnter_t>(SteamMatchmaking.JoinLobby(lobby), _lifetimeSource.Token);
                if (outcome.IOFailure)
                    throw new InvalidOperationException("Steam failed while joining the lobby.");

                var enterResponse = (EChatRoomEnterResponse)outcome.Value.m_EChatRoomEnterResponse;
                if (enterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                    ThrowJoinFailure(enterResponse);

                if (cancellationToken.IsCancellationRequested)
                {
                    SteamMatchmaking.LeaveLobby(lobby);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                _currentLobby = lobby;
                LoadJoinedLobbyData();
                if (SteamMatchmaking.GetLobbyOwner(_currentLobby).m_SteamID != _originalHostId)
                    throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);

                _localPlayerFields = playerFields;
                _localPlayerNickname = playerNickname;
                AcceptedPlayerIds.Clear();
                BlockedPlayerIds.Clear();
                PublishLocalPlayerData();
                _state = StateEnum.AwaitingAdmission;
                var admissionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _admissionSource = admissionSource;
                if (SendMessage(_originalHostId, MessageKind.AdmissionRequest, admissionPayload) == false)
                    throw new InvalidOperationException("Steam rejected the room admission request.");

                var admitted = await WaitForAdmissionAsync(admissionSource.Task, cancellationToken);
                if (admitted == false)
                    throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotPermitted);

                cancellationToken.ThrowIfCancellationRequested();
                _admissionSource = null;
                _state = StateEnum.Member;
                return BuildCurrentRoom();
            }
            catch
            {
                if (_currentLobby.m_SteamID != 0)
                    ResetSession(true, StateEnum.Ready);
                else
                    _state = StateEnum.Ready;

                throw;
            }
        }

        private void ExitRoomCore(string roomId, string playerId)
        {
            EnsureCurrentRoom(roomId);
            if (ulong.TryParse(playerId, out var targetId) == false)
                throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);

            if (AcceptedPlayerIds.Contains(targetId) == false)
                throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);

            if (_state == StateEnum.Host)
            {
                if (targetId == _localSteamId)
                {
                    CloseHostedRoom();
                    return;
                }

                AcceptedPlayerIds.Remove(targetId);
                BlockedPlayerIds.Add(targetId);
                LogicalPlayers.Remove(targetId);
                PublishRoster();
                BroadcastRoster();
                SendLobbyControl(MessageKind.PlayerKicked, targetId);
                SendMessage(targetId, MessageKind.PlayerKicked, EncodeUInt64(targetId));
                return;
            }

            if ((_state != StateEnum.Member) || (targetId != _localSteamId))
                throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotPermitted);

            _state = StateEnum.Leaving;
            ResetSession(true, StateEnum.Ready);
        }

        private void EnsureCurrentRoom(string roomId)
        {
            if ((_currentLobby.m_SteamID == 0) || (_currentLobby.m_SteamID.ToString() != roomId))
                throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);
        }

        private static void ThrowCreateFailure(EResult result)
        {
            switch (result)
            {
                case EResult.k_EResultLimitExceeded:
                    throw new BusyException();
                case EResult.k_EResultAccessDenied:
                    throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotPermitted);
                default:
                    throw new InvalidOperationException($"Steam failed to create the lobby ({result}).");
            }
        }

        private static void ThrowJoinFailure(EChatRoomEnterResponse response)
        {
            switch (response)
            {
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist:
                    throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed:
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseFull:
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseBanned:
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseLimited:
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseClanDisabled:
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseCommunityBan:
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseMemberBlockedYou:
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseYouBlockedMember:
                    throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotPermitted);
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseRatelimitExceeded:
                    throw new BusyException();
                default:
                    throw new InvalidOperationException($"Steam failed to enter the lobby ({response}).");
            }
        }

        private async Task<CallOutcome<T>> WaitForCallAsync<T>(SteamAPICall_t apiCall, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (apiCall == SteamAPICall_t.Invalid)
                throw new InvalidOperationException("Steam returned an invalid API call handle.");

            var source = new TaskCompletionSource<CallOutcome<T>>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var callResult = CallResult<T>.Create((value, ioFailure) => source.TrySetResult(new CallOutcome<T>(value, ioFailure))))
            {
                callResult.Set(apiCall);
                using (cancellationToken.Register(() => source.TrySetCanceled()))
                    return await source.Task;
            }
        }

        private async Task<bool> RequestLobbyDataAsync(CSteamID lobby, CancellationToken cancellationToken)
        {
            var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (PendingLobbyData.ContainsKey(lobby.m_SteamID))
                throw new BusyException();

            PendingLobbyData.Add(lobby.m_SteamID, source);
            try
            {
                if (SteamMatchmaking.RequestLobbyData(lobby) == false)
                    throw new InvalidOperationException("Steam is not connected and could not request lobby data.");

                using (cancellationToken.Register(() => source.TrySetCanceled()))
                    return await source.Task;
            }
            finally
            {
                PendingLobbyData.Remove(lobby.m_SteamID);
            }
        }

        private async Task<bool> WaitForAdmissionAsync(Task<bool> admissionTask, CancellationToken cancellationToken)
        {
            var timeoutTask = Task.Delay(AdmissionTimeoutMilliseconds, cancellationToken);
            if (await Task.WhenAny(admissionTask, timeoutTask) == admissionTask)
                return await admissionTask;

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("Steam room admission timed out.");
        }

        private async Task<PlayerUpdateOutcomeEnum> WaitForPlayerUpdateAsync(Task<PlayerUpdateOutcomeEnum> updateTask, CancellationToken cancellationToken)
        {
            var timeoutTask = Task.Delay(AdmissionTimeoutMilliseconds, cancellationToken);
            if (await Task.WhenAny(updateTask, timeoutTask) == updateTask)
                return await updateTask;

            cancellationToken.ThrowIfCancellationRequested();
            return PlayerUpdateOutcomeEnum.Unknown;
        }

        private static CSteamID ParseLobby(string roomId, string code)
        {
            ulong value;
            if (string.IsNullOrWhiteSpace(roomId) == false)
            {
                if (ulong.TryParse(roomId, out value) == false)
                    throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);
            }
            else if (TryDecodeCode(code, out value) == false)
            {
                throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);
            }

            var lobby = new CSteamID(value);
            if ((lobby.IsValid() == false) || (lobby.IsLobby() == false))
                throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);

            return lobby;
        }

        private void ValidateLobbyForJoin(CSteamID lobby)
        {
            if (SteamMatchmaking.GetLobbyData(lobby, MetadataSchema) != ProtocolVersion.ToString())
                throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);

            if (ReadBooleanLobbyData(lobby, MetadataClosed))
                throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);

            if (ReadBooleanLobbyData(lobby, MetadataIsLocked))
                throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotPermitted);

            var hostText = SteamMatchmaking.GetLobbyData(lobby, MetadataHost);
            var epoch = SteamMatchmaking.GetLobbyData(lobby, MetadataEpoch);
            if ((ulong.TryParse(hostText, out _) == false) || string.IsNullOrWhiteSpace(epoch))
                throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);

        }

        private void LoadJoinedLobbyData()
        {
            if (ulong.TryParse(SteamMatchmaking.GetLobbyData(_currentLobby, MetadataHost), out _originalHostId) == false)
                throw new FormatException("Steam lobby host metadata is invalid.");

            _epoch = SteamMatchmaking.GetLobbyData(_currentLobby, MetadataEpoch);
            _title = SteamMatchmaking.GetLobbyData(_currentLobby, MetadataTitle) ?? string.Empty;
            _isLocked = ReadBooleanLobbyData(_currentLobby, MetadataIsLocked);
            _isPrivate = ReadBooleanLobbyData(_currentLobby, MetadataIsPrivate);
            _maxPlayers = ParseBoundedInt(SteamMatchmaking.GetLobbyData(_currentLobby, MetadataMaxPlayers), 1, PlayerCountMax);
            _password = null;
            _roomFields = Array.Empty<MyNetInterface.Field>();
            _memberRoomFields = Array.Empty<MyNetInterface.Field>();
        }

        private void PublishRoomData()
        {
            if (_state != StateEnum.Creating)
            {
                var type = _isPrivate ? ELobbyType.k_ELobbyTypeInvisible : ELobbyType.k_ELobbyTypePublic;
                if (SteamMatchmaking.SetLobbyType(_currentLobby, type) == false)
                    throw new InvalidOperationException("Steam rejected the lobby type.");
            }

            SetLobbyData(MetadataSchema, ProtocolVersion.ToString());
            SetLobbyData(MetadataEpoch, _epoch);
            SetLobbyData(MetadataHost, _originalHostId.ToString());
            SetLobbyData(MetadataTitle, _title);
            SetLobbyData(MetadataIsPrivate, _isPrivate ? "1" : "0");
            SetLobbyData(MetadataIsLocked, _isLocked ? "1" : "0");
            SetLobbyData(MetadataMaxPlayers, _maxPlayers.ToString());
            SetLobbyData(MetadataHasPassword, string.IsNullOrEmpty(_password) ? "0" : "1");
            SetLobbyData(MetadataRoomFields, EncodeFields(SelectFields(_roomFields, MyNetInterface.Field.VisibilityEnum.Public)));
        }

        private void PublishLocalPlayerData()
        {
            SetLobbyMemberData(MetadataPlayerNickname, _localPlayerNickname ?? string.Empty);
            SetLobbyMemberData(MetadataPlayerFields, EncodeFields(SelectFields(_localPlayerFields, MyNetInterface.Field.VisibilityEnum.Public)));
        }

        private void PublishRoster()
        {
            if (_state != StateEnum.Creating && _state != StateEnum.Host)
                return;

            var encoded = EncodeRoster(BuildLogicalRoster(false));
            var chunkCount = Math.Max(1, (encoded.Length + RosterChunkCharacterCount - 1) / RosterChunkCharacterCount);
            if (chunkCount > RosterChunkCountMax)
                throw new FormatException("Steam public roster metadata is too large.");

            var previousChunkCountText = SteamMatchmaking.GetLobbyData(_currentLobby, MetadataRosterChunkCount);
            var previousChunkCount = string.IsNullOrEmpty(previousChunkCountText) ? 0 : ParseBoundedInt(previousChunkCountText, 1, RosterChunkCountMax);
            var revision = Guid.NewGuid().ToString("N");
            for (var index = 0; index < chunkCount; ++index)
            {
                var offset = index * RosterChunkCharacterCount;
                var length = Math.Min(RosterChunkCharacterCount, encoded.Length - offset);
                var chunk = length > 0 ? encoded.Substring(offset, length) : string.Empty;
                SetLobbyData(MetadataRosterChunkPrefix + index, revision + ":" + chunk);
            }

            SetLobbyData(MetadataRosterChunkCount, chunkCount.ToString());
            SetLobbyData(MetadataRosterRevision, revision);
            for (var index = chunkCount; index < previousChunkCount; ++index)
                SteamMatchmaking.DeleteLobbyData(_currentLobby, MetadataRosterChunkPrefix + index);
        }

        private MyNetRoomInterface BuildCurrentRoom()
        {
            if (_currentLobby.m_SteamID == 0)
                throw new FailureException(MyNetInterface.CatchInterface.FailureEnum.NotFoundRoom);

            MyNetInterface.Field[] roomFields;
            if (_state == StateEnum.Host)
            {
                roomFields = CloneFields(_roomFields);
            }
            else
            {
                var publicFields = DecodeFields(SteamMatchmaking.GetLobbyData(_currentLobby, MetadataRoomFields));
                roomFields = MergeFields(publicFields, _memberRoomFields);
            }

            var players = new List<MyNetPlayerInterface>();
            foreach (var playerId in GetOrderedAcceptedPlayerIds())
            {
                MyNetInterface.Field[] fields;
                string nickname;
                if (playerId == _localSteamId)
                {
                    fields = CloneFields(_localPlayerFields);
                    nickname = _localPlayerNickname;
                }
                else if (LogicalPlayers.TryGetValue(playerId, out var player))
                {
                    fields = CloneFields(player.Fields);
                    nickname = player.Nickname;
                }
                else
                {
                    throw new FormatException("Steam logical player data is missing.");
                }

                players.Add(new SteamNetPlayer(fields, playerId.ToString(), playerId == _originalHostId, nickname));
            }

            return new SteamNetRoom(EncodeCode(_currentLobby.m_SteamID), roomFields, ReadBooleanLobbyData(_currentLobby, MetadataHasPassword), _originalHostId.ToString(), _currentLobby.m_SteamID.ToString(), _isLocked, _isPrivate, _maxPlayers, players.ToArray(), _title);
        }

        private static MyNetRoomInterface BuildSearchRoom(CSteamID lobby)
        {
            if ((lobby.IsValid() == false) || (lobby.IsLobby() == false))
                throw new FormatException("Steam returned an invalid lobby ID.");

            if (SteamMatchmaking.GetLobbyData(lobby, MetadataSchema) != ProtocolVersion.ToString())
                throw new FormatException("Steam lobby protocol metadata is missing.");

            if (ReadBooleanLobbyData(lobby, MetadataIsPrivate) || ReadBooleanLobbyData(lobby, MetadataClosed))
                throw new FormatException("Steam lobby is not visible in the public list.");

            if (ulong.TryParse(SteamMatchmaking.GetLobbyData(lobby, MetadataHost), out var hostId) == false)
                throw new FormatException("Steam lobby host metadata is invalid.");

            var roster = ReadRoster(lobby);
            var players = new MyNetPlayerInterface[roster.Count];
            for (var index = 0; index < roster.Count; ++index)
            {
                var player = roster[index];
                players[index] = new SteamNetPlayer(player.Fields, player.Id.ToString(), player.Id == hostId, player.Nickname);
            }

            var maxPlayers = ParseBoundedInt(SteamMatchmaking.GetLobbyData(lobby, MetadataMaxPlayers), 1, PlayerCountMax);
            if (players.Length > maxPlayers)
                throw new FormatException("Steam lobby roster exceeds its player limit.");

            return new SteamNetRoom(EncodeCode(lobby.m_SteamID), DecodeFields(SteamMatchmaking.GetLobbyData(lobby, MetadataRoomFields)), ReadBooleanLobbyData(lobby, MetadataHasPassword), hostId.ToString(), lobby.m_SteamID.ToString(), ReadBooleanLobbyData(lobby, MetadataIsLocked), ReadBooleanLobbyData(lobby, MetadataIsPrivate), maxPlayers, players, SteamMatchmaking.GetLobbyData(lobby, MetadataTitle) ?? string.Empty);
        }

        private void BroadcastRoster(MessageKind kind = MessageKind.RosterChanged, ulong updatedPlayerId = 0, ulong excludedPlayerId = 0)
        {
            var payload = EncodeMemberSnapshot();
            if (kind == MessageKind.PlayerUpdated)
                payload = EncodePlayerUpdated(updatedPlayerId, payload);

            PendingRosterPlayerIds.Clear();
            _pendingRosterKind = kind;
            _pendingRosterPayload = payload;
            foreach (var playerId in AcceptedPlayerIds)
            {
                if ((playerId != _localSteamId) && (playerId != excludedPlayerId) && (SendMessage(playerId, kind, payload) == false))
                    PendingRosterPlayerIds.Add(playerId);
            }

            if (PendingRosterPlayerIds.Count == 0)
                _pendingRosterPayload = null;
        }

        private void FlushPendingRoster()
        {
            if ((_state != StateEnum.Host) || (_pendingRosterPayload == null) || (PendingRosterPlayerIds.Count == 0))
                return;

            foreach (var playerId in new List<ulong>(PendingRosterPlayerIds))
            {
                if ((AcceptedPlayerIds.Contains(playerId) == false) || SendMessage(playerId, _pendingRosterKind, _pendingRosterPayload))
                    PendingRosterPlayerIds.Remove(playerId);
            }

            if (PendingRosterPlayerIds.Count == 0)
                _pendingRosterPayload = null;
        }

        private void CloseHostedRoom()
        {
            _state = StateEnum.Leaving;
            try
            {
                SteamMatchmaking.SetLobbyJoinable(_currentLobby, false);
                SetLobbyData(MetadataClosed, "1");
                SendLobbyControl(MessageKind.RoomClosed, 0);
                foreach (var playerId in AcceptedPlayerIds)
                {
                    if (playerId != _localSteamId)
                        SendMessage(playerId, MessageKind.RoomClosed, Array.Empty<byte>());
                }
            }
            finally
            {
                ResetSession(true, StateEnum.Ready);
            }
        }

        private void ResetSession(bool leaveLobby, StateEnum nextState)
        {
            _state = nextState;
            var lobby = _currentLobby;
            var peers = new HashSet<ulong>(AcceptedPlayerIds);
            peers.UnionWith(BlockedPlayerIds);
            if (_originalHostId != 0)
                peers.Add(_originalHostId);

            _admissionSource?.TrySetResult(false);
            _admissionSource = null;
            _playerUpdateSource?.TrySetResult(PlayerUpdateOutcomeEnum.Unknown);
            _playerUpdateSource = null;
            _pendingPlayerUpdateId = 0;
            _chatRoomId = null;
            _currentLobby = CSteamID.Nil;
            _originalHostId = 0;
            _epoch = null;
            _password = null;
            _title = null;
            _isLocked = false;
            _isPrivate = false;
            _maxPlayers = 0;
            _roomFields = Array.Empty<MyNetInterface.Field>();
            _memberRoomFields = Array.Empty<MyNetInterface.Field>();
            _localPlayerFields = Array.Empty<MyNetInterface.Field>();
            _localPlayerNickname = null;
            AcceptedPlayerIds.Clear();
            BlockedPlayerIds.Clear();
            LogicalPlayers.Clear();
            PendingRosterPlayerIds.Clear();
            _pendingRosterPayload = null;
            IncomingRequests.Clear();
            IncomingResponses.Clear();
            lock (OutgoingRequestLock)
                OutgoingRequests.Clear();

            lock (OutgoingResponseLock)
                OutgoingResponses.Clear();

            try
            {
                if (leaveLobby && (lobby.m_SteamID != 0))
                    SteamMatchmaking.LeaveLobby(lobby);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to leave Steam lobby during cleanup: {exception.Message}");
            }

            foreach (var peer in peers)
            {
                if (peer == _localSteamId)
                    continue;

                try
                {
                    ClosePeer(peer);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Failed to close Steam peer channel during cleanup: {exception.Message}");
                }
            }
        }

        private void UpdateLobbyPolling()
        {
            var config = _lobbyPollingConfig;
            if ((config == null) || _isLobbyPolling || (_currentLobby.m_SteamID != 0) || (Time.realtimeSinceStartup < _nextLobbyPollTimeSeconds))
                return;

            _nextLobbyPollTimeSeconds = Time.realtimeSinceStartup + Math.Max(MinimumPollingDelaySeconds, config.PollingDelaySeconds);
            PollLobbyAsync(config, _lobbyPollingResult, _lobbyPollingGeneration);
        }

        private async void PollLobbyAsync(MyNetLobbyServiceInterface.ConfigInterface config, MyNetLobbyServiceInterface.ResultInterface result, int generation)
        {
            _isLobbyPolling = true;
            try
            {
                using (var cancellationSource = CreateCancellationSource(config.CancellationToken))
                {
                    var cancellationToken = cancellationSource.Token;
                    if (await OperationGate.WaitAsync(0, cancellationToken) == false)
                        return;

                    MyNetRoomInterface[] rooms;
                    try
                    {
                        rooms = await RefreshLobbyCoreAsync(cancellationToken);
                    }
                    finally
                    {
                        OperationGate.Release();
                    }

                    if ((_lobbyPollingGeneration == generation) && (_lobbyPollingConfig == config) && (_currentLobby.m_SteamID == 0))
                    {
                        try
                        {
                            result.OnOk(rooms);
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if ((_lobbyPollingGeneration == generation) && (_lobbyPollingConfig == config))
                {
                    try
                    {
                        result.OnException(new MyNetSessionException("Failed to update Steam lobby.", exception));
                    }
                    catch (Exception callbackException)
                    {
                        Debug.LogException(callbackException);
                    }
                }
            }
            finally
            {
                _isLobbyPolling = false;
            }
        }

        private void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
        {
            try
            {
                if (PendingLobbyData.TryGetValue(callback.m_ulSteamIDLobby, out var source))
                    source.TrySetResult(callback.m_bSuccess != 0);

                if ((_currentLobby.m_SteamID == 0) || (callback.m_ulSteamIDLobby != _currentLobby.m_SteamID))
                    return;

                if (callback.m_ulSteamIDMember == callback.m_ulSteamIDLobby)
                {
                    if (ReadBooleanLobbyData(_currentLobby, MetadataClosed) || (SteamMatchmaking.GetLobbyOwner(_currentLobby).m_SteamID != _originalHostId))
                    {
                        ResetSession(true, StateEnum.Ready);
                        return;
                    }

                    _isPrivate = ReadBooleanLobbyData(_currentLobby, MetadataIsPrivate);
                    _isLocked = ReadBooleanLobbyData(_currentLobby, MetadataIsLocked);
                    _title = SteamMatchmaking.GetLobbyData(_currentLobby, MetadataTitle) ?? string.Empty;
                    _maxPlayers = ParseBoundedInt(SteamMatchmaking.GetLobbyData(_currentLobby, MetadataMaxPlayers), 1, PlayerCountMax);

                    return;
                }

            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
        {
            try
            {
                if ((_currentLobby.m_SteamID == 0) || (callback.m_ulSteamIDLobby != _currentLobby.m_SteamID))
                    return;

                var changes = (EChatMemberStateChange)callback.m_rgfChatMemberStateChange;
                var left = (changes & (EChatMemberStateChange.k_EChatMemberStateChangeLeft | EChatMemberStateChange.k_EChatMemberStateChangeDisconnected | EChatMemberStateChange.k_EChatMemberStateChangeKicked | EChatMemberStateChange.k_EChatMemberStateChangeBanned)) != 0;
                if (left == false)
                    return;

                var playerId = callback.m_ulSteamIDUserChanged;
                BlockedPlayerIds.Remove(playerId);
                var wasAccepted = AcceptedPlayerIds.Remove(playerId);
                LogicalPlayers.Remove(playerId);
                if (playerId == _localSteamId)
                {
                    ResetSession(false, StateEnum.Ready);
                    return;
                }

                if (_state == StateEnum.Host)
                {
                    if (wasAccepted)
                    {
                        PublishRoster();
                        BroadcastRoster();
                    }

                    ClosePeer(playerId);
                }

                if ((_currentLobby.m_SteamID != 0) && (SteamMatchmaking.GetLobbyOwner(_currentLobby).m_SteamID != _originalHostId))
                    ResetSession(true, StateEnum.Ready);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void OnLobbyChatMessage(LobbyChatMsg_t callback)
        {
            try
            {
                if ((_currentLobby.m_SteamID == 0) || (callback.m_ulSteamIDLobby != _currentLobby.m_SteamID) || (callback.m_iChatID > int.MaxValue))
                    return;

                var data = new byte[4096];
                var length = SteamMatchmaking.GetLobbyChatEntry(_currentLobby, (int)callback.m_iChatID, out var sender, data, data.Length, out var entryType);
                if ((length <= 0) || (entryType != EChatEntryType.k_EChatEntryTypeChatMsg))
                    return;

                using (var stream = new MemoryStream(data, 0, length, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    var magic = reader.ReadUInt32();
                    if (reader.ReadByte() != ProtocolVersion)
                        return;

                    if (magic == ChatMagic)
                    {
                        var roomId = _currentLobby.m_SteamID.ToString();
                        if ((_chatRoomId != roomId) || (AcceptedPlayerIds.Contains(sender.m_SteamID) == false))
                            return;

                        var message = reader.ReadString();
                        if ((Encoding.UTF8.GetByteCount(message) > SteamNetChatService.MessageByteCountMax) || (stream.Position != stream.Length))
                            return;

                        _chatResult.OnReceived(message, sender.m_SteamID.ToString(), roomId);
                        return;
                    }

                    if ((magic != ControlMagic) || (sender.m_SteamID != _originalHostId))
                        return;

                    var kind = (MessageKind)reader.ReadByte();
                    var targetId = reader.ReadUInt64();
                    var epoch = reader.ReadString();
                    if ((epoch != _epoch) || (stream.Position != stream.Length))
                        return;

                    if ((kind == MessageKind.RoomClosed) || (((kind == MessageKind.PlayerKicked) || (kind == MessageKind.AdmissionRejected)) && (targetId == _localSteamId)))
                        ResetSession(true, StateEnum.Ready);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Dropped invalid Steam lobby message: {exception.Message}");
            }
        }

        private static byte[] EncodeChat(string message)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(ChatMagic);
                    writer.Write((byte)ProtocolVersion);
                    writer.Write(message);
                }

                if (stream.Length > 4096)
                    throw new FormatException("Steam chat message exceeds the lobby message size limit.");

                return stream.ToArray();
            }
        }

        private bool SendLobbyControl(MessageKind kind, ulong targetId)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(ControlMagic);
                    writer.Write((byte)ProtocolVersion);
                    writer.Write((byte)kind);
                    writer.Write(targetId);
                    writer.Write(_epoch);
                }

                var data = stream.ToArray();
                var sent = SteamMatchmaking.SendLobbyChatMsg(_currentLobby, data, data.Length);
                if (sent == false)
                    Debug.LogWarning("Steam rejected an Oplat lobby control message.");

                return sent;
            }
        }

        private void OnMessageSessionRequest(SteamNetworkingMessagesSessionRequest_t callback)
        {
            try
            {
                if (_currentLobby.m_SteamID == 0)
                    return;

                var identity = callback.m_identityRemote;
                var steamId = identity.GetSteamID();
                if ((steamId.IsValid() == false) || (steamId.BIndividualAccount() == false))
                    return;

                var accepted = false;
                if ((_state == StateEnum.Host) || (_state == StateEnum.Creating))
                    accepted = BlockedPlayerIds.Contains(steamId.m_SteamID) == false && IsRawLobbyMember(steamId.m_SteamID);
                else if ((_state == StateEnum.AwaitingAdmission) || (_state == StateEnum.Member))
                    accepted = steamId.m_SteamID == _originalHostId;

                if (accepted)
                    SteamNetworkingMessages.AcceptSessionWithUser(ref identity);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void OnMessageSessionFailed(SteamNetworkingMessagesSessionFailed_t callback)
        {
            var identity = callback.m_info.m_identityRemote;
            var playerId = identity.GetSteamID().m_SteamID;
            SteamNetworkingMessages.CloseChannelWithUser(ref identity, MessageChannel);
            if ((_state == StateEnum.AwaitingAdmission) && (playerId == _originalHostId))
                _admissionSource?.TrySetResult(false);

            if ((_state == StateEnum.Member) && (playerId == _originalHostId))
                _playerUpdateSource?.TrySetResult(PlayerUpdateOutcomeEnum.Unknown);
        }

        private void ReceiveMessages()
        {
            var pointers = new IntPtr[MessagesPerFrameMax];
            var count = SteamNetworkingMessages.ReceiveMessagesOnChannel(MessageChannel, pointers, pointers.Length);
            var totalByteCount = 0;
            for (var index = 0; index < count; ++index)
            {
                var pointer = pointers[index];
                try
                {
                    var message = SteamNetworkingMessage_t.FromIntPtr(pointer);
                    if ((message.m_cbSize <= 0) || (message.m_cbSize > MessageByteCountMax) || (message.m_pData == IntPtr.Zero))
                        continue;

                    totalByteCount += message.m_cbSize;
                    if (totalByteCount > MessageByteCountMax * 4)
                        continue;

                    var data = new byte[message.m_cbSize];
                    Marshal.Copy(message.m_pData, data, 0, data.Length);
                    HandleMessage(message.m_identityPeer.GetSteamID().m_SteamID, data);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Dropped invalid Steam P2P message: {exception.Message}");
                }
                finally
                {
                    if (pointer != IntPtr.Zero)
                        SteamNetworkingMessage_t.Release(pointer);
                }
            }
        }

        private void HandleMessage(ulong senderId, byte[] data)
        {
            if ((_currentLobby.m_SteamID == 0) || (TryReadMessage(data, out var kind, out var lobbyId, out var epoch, out var payload) == false) || (lobbyId != _currentLobby.m_SteamID) || (epoch != _epoch))
                return;

            if ((_state == StateEnum.Host) && BlockedPlayerIds.Contains(senderId))
                return;

            switch (kind)
            {
                case MessageKind.AdmissionRequest:
                    if (_state == StateEnum.Host)
                        HandleAdmissionRequest(senderId, payload);
                    break;
                case MessageKind.AdmissionAccepted:
                    if ((_state == StateEnum.AwaitingAdmission) && (senderId == _originalHostId))
                    {
                        ApplyMemberSnapshot(payload);
                        var isAccepted = AcceptedPlayerIds.Contains(_localSteamId);
                        if (isAccepted)
                            _state = StateEnum.Member;

                        _admissionSource?.TrySetResult(isAccepted);
                    }
                    break;
                case MessageKind.AdmissionRejected:
                    if ((_state == StateEnum.AwaitingAdmission) && (senderId == _originalHostId))
                        _admissionSource?.TrySetResult(false);
                    break;
                case MessageKind.RosterChanged:
                    if ((_state == StateEnum.Member) && (senderId == _originalHostId))
                    {
                        ApplyMemberSnapshot(payload);
                        if (AcceptedPlayerIds.Contains(_localSteamId) == false)
                            ResetSession(true, StateEnum.Ready);
                        else
                            _roomResult.OnOk(BuildCurrentRoom());
                    }
                    break;
                case MessageKind.PlayerDataChanged:
                    if ((_state == StateEnum.Host) && AcceptedPlayerIds.Contains(senderId) && IsRawLobbyMember(senderId))
                    {
                        var updateId = 0ul;
                        var hadPreviousPlayer = LogicalPlayers.TryGetValue(senderId, out var previousPlayer);
                        try
                        {
                            LogicalPlayers[senderId] = DecodePlayerUpdate(senderId, payload, out updateId);
                            EncodeMemberSnapshot();
                            PublishRoster();
                            BroadcastRoster(MessageKind.PlayerUpdated, senderId, senderId);
                            if (SendMessage(senderId, MessageKind.PlayerDataAccepted, EncodeUInt64(updateId)) == false)
                                throw new InvalidOperationException("Steam rejected the player update acknowledgement.");
                        }
                        catch (Exception exception)
                        {
                            if (hadPreviousPlayer)
                                LogicalPlayers[senderId] = previousPlayer;
                            else
                                LogicalPlayers.Remove(senderId);

                            try
                            {
                                PublishRoster();
                                BroadcastRoster();
                            }
                            catch (Exception cleanupException)
                            {
                                Debug.LogWarning($"Failed to republish rejected Steam player data: {cleanupException.Message}");
                            }

                            if (updateId != 0)
                                SendMessage(senderId, MessageKind.PlayerDataRejected, EncodeUInt64(updateId));

                            Debug.LogWarning($"Rejected Steam player data update: {exception.Message}");
                        }
                    }
                    break;
                case MessageKind.PlayerUpdated:
                    if ((_state == StateEnum.Member) && (senderId == _originalHostId))
                    {
                        var playerId = DecodePlayerUpdated(payload, out var snapshot);
                        ApplyMemberSnapshot(snapshot);
                        if (AcceptedPlayerIds.Contains(_localSteamId) == false)
                            ResetSession(true, StateEnum.Ready);
                        else
                            _playerResult.OnOk(BuildCurrentRoom().Players.First(value => value.Id == playerId.ToString()));
                    }
                    break;
                case MessageKind.PlayerDataAccepted:
                case MessageKind.PlayerDataRejected:
                    if ((_state == StateEnum.Member) && (senderId == _originalHostId) && (DecodeUInt64(payload) == _pendingPlayerUpdateId))
                        _playerUpdateSource?.TrySetResult(kind == MessageKind.PlayerDataAccepted ? PlayerUpdateOutcomeEnum.Accepted : PlayerUpdateOutcomeEnum.Rejected);
                    break;
                case MessageKind.Request:
                    if ((_state == StateEnum.Host) && AcceptedPlayerIds.Contains(senderId) && IsRawLobbyMember(senderId) && (IncomingRequests.Count < MessageQueueCountMax))
                        IncomingRequests.Enqueue(DeserializePayload<MyNetRequest>(payload));
                    break;
                case MessageKind.Response:
                    if ((_state == StateEnum.Member) && (senderId == _originalHostId) && (IncomingResponses.Count < MessageQueueCountMax))
                        IncomingResponses.Enqueue(DeserializePayload<MyNetResponse>(payload));
                    break;
                case MessageKind.RoomClosed:
                    if (senderId == _originalHostId)
                        ResetSession(true, StateEnum.Ready);
                    break;
                case MessageKind.PlayerKicked:
                    if ((senderId == _originalHostId) && (DecodeUInt64(payload) == _localSteamId))
                        ResetSession(true, StateEnum.Ready);
                    break;
            }
        }

        private void HandleAdmissionRequest(ulong senderId, byte[] payload)
        {
            if ((senderId == _localSteamId) || BlockedPlayerIds.Contains(senderId) || (IsRawLobbyMember(senderId) == false))
                return;

            var player = DecodeAdmissionRequest(senderId, payload, out var suppliedPassword);
            if (AcceptedPlayerIds.Contains(senderId))
            {
                SendMessage(senderId, MessageKind.AdmissionAccepted, EncodeMemberSnapshot());
                return;
            }

            if (_isLocked || (AcceptedPlayerIds.Count >= _maxPlayers) || (suppliedPassword != (_password ?? string.Empty)))
            {
                BlockedPlayerIds.Add(senderId);
                var sentControl = SendLobbyControl(MessageKind.AdmissionRejected, senderId);
                SendMessage(senderId, MessageKind.AdmissionRejected, Array.Empty<byte>());
                if (sentControl)
                    ClosePeer(senderId);

                return;
            }

            try
            {
                AcceptedPlayerIds.Add(senderId);
                LogicalPlayers[senderId] = player;
                PublishRoster();
                if (SendMessage(senderId, MessageKind.AdmissionAccepted, EncodeMemberSnapshot()) == false)
                    throw new InvalidOperationException("Steam rejected the room admission response.");

                BroadcastRoster();
            }
            catch (Exception exception)
            {
                AcceptedPlayerIds.Remove(senderId);
                LogicalPlayers.Remove(senderId);
                SendMessage(senderId, MessageKind.AdmissionRejected, Array.Empty<byte>());
                try
                {
                    PublishRoster();
                    BroadcastRoster();
                }
                catch (Exception cleanupException)
                {
                    Debug.LogWarning($"Failed to republish Steam room admission cleanup: {cleanupException.Message}");
                }

                Debug.LogWarning($"Rejected Steam room admission: {exception.Message}");
            }
        }

        private void FlushResponses()
        {
            var useLocal = _useLocal;
            if ((useLocal == false) && (_state != StateEnum.Host))
                return;

            for (var index = 0; index < MessagesPerFrameMax; ++index)
            {
                MyNetResponse response;
                lock (OutgoingResponseLock)
                {
                    if (OutgoingResponses.Count == 0)
                        return;

                    response = OutgoingResponses.Dequeue();
                }

                if (useLocal)
                {
                    if (IncomingResponses.Count < MessageQueueCountMax)
                        IncomingResponses.Enqueue(response);

                    continue;
                }

                try
                {
                    var payload = MyNetSerializer.Serialize(response);
                    if (payload.Length > MessageByteCountMax)
                        throw new FormatException("Steam response exceeds the message size limit.");

                    if (IncomingResponses.Count < MessageQueueCountMax)
                        IncomingResponses.Enqueue(response);

                    foreach (var playerId in AcceptedPlayerIds)
                    {
                        if (playerId != _localSteamId)
                            SendMessage(playerId, MessageKind.Response, payload);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Dropped Steam response: {exception.Message}");
                }
            }
        }

        private void HandleResponses()
        {
            if (IncomingResponses.Count == 0)
                return;

            while (IncomingResponses.Count > 0)
            {
                try
                {
                    _memberResult.OnReceived(IncomingResponses.Dequeue());
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            try
            {
                _memberResult.OnFinishThisHandling();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void FlushRequests()
        {
            var useLocal = _useLocal;
            var state = _state;
            if ((useLocal == false) && (state != StateEnum.Host) && (state != StateEnum.Member))
                return;

            for (var index = 0; index < MessagesPerFrameMax; ++index)
            {
                MyNetRequest request;
                lock (OutgoingRequestLock)
                {
                    if (OutgoingRequests.Count == 0)
                        return;

                    request = OutgoingRequests.Peek();
                }

                var removeRequest = false;
                try
                {
                    if (useLocal || (state == StateEnum.Host))
                    {
                        if (IncomingRequests.Count >= MessageQueueCountMax)
                            return;

                        IncomingRequests.Enqueue(request);
                        removeRequest = true;
                    }
                    else
                    {
                        var payload = MyNetSerializer.Serialize(request);
                        if (payload.Length > MessageByteCountMax)
                            throw new FormatException("Steam request exceeds the message size limit.");

                        if (SendMessage(_originalHostId, MessageKind.Request, payload) == false)
                            return;

                        removeRequest = true;
                    }
                }
                catch (Exception exception)
                {
                    removeRequest = true;
                    Debug.LogWarning($"Dropped Steam request: {exception.Message}");
                }
                finally
                {
                    if (removeRequest)
                    {
                        lock (OutgoingRequestLock)
                        {
                            if ((OutgoingRequests.Count > 0) && ReferenceEquals(OutgoingRequests.Peek(), request))
                                OutgoingRequests.Dequeue();
                        }
                    }
                }
            }
        }

        private void HandleRequests()
        {
            if (IncomingRequests.Count == 0)
                return;

            while (IncomingRequests.Count > 0)
            {
                try
                {
                    _hostResult.OnReceived(IncomingRequests.Dequeue());
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            try
            {
                _hostResult.OnFinishThisHandling();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private bool SendMessage(ulong playerId, MessageKind kind, byte[] payload)
        {
            if ((_currentLobby.m_SteamID == 0) || (playerId == 0) || (playerId == _localSteamId))
                return false;

            var data = WriteMessage(kind, payload);
            var identity = default(SteamNetworkingIdentity);
            identity.SetSteamID(new CSteamID(playerId));
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                var sendFlags = Constants.k_nSteamNetworkingSend_Reliable | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession;
                var result = SteamNetworkingMessages.SendMessageToUser(ref identity, handle.AddrOfPinnedObject(), checked((uint)data.Length), sendFlags, MessageChannel);

                if (result != EResult.k_EResultOK)
                {
                    Debug.LogWarning($"Steam rejected an Oplat P2P message ({result}).");
                    return false;
                }

                return true;
            }
            finally
            {
                handle.Free();
            }
        }

        private byte[] WriteMessage(MessageKind kind, byte[] payload)
        {
            payload ??= Array.Empty<byte>();
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(MessageMagic);
                    writer.Write((byte)ProtocolVersion);
                    writer.Write((byte)kind);
                    writer.Write(_currentLobby.m_SteamID);
                    writer.Write(_epoch);
                    writer.Write(payload.Length);
                    writer.Write(payload);
                }

                if (stream.Length > MessageByteCountMax)
                    throw new FormatException("Steam P2P message exceeds the configured size limit.");

                return stream.ToArray();
            }
        }

        private static bool TryReadMessage(byte[] data, out MessageKind kind, out ulong lobbyId, out string epoch, out byte[] payload)
        {
            kind = default;
            lobbyId = 0;
            epoch = null;
            payload = null;
            if ((data == null) || (data.Length <= 0) || (data.Length > MessageByteCountMax))
                return false;

            try
            {
                using (var stream = new MemoryStream(data, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    if ((reader.ReadUInt32() != MessageMagic) || (reader.ReadByte() != ProtocolVersion))
                        return false;

                    kind = (MessageKind)reader.ReadByte();
                    if ((kind < MessageKind.AdmissionRequest) || (kind > MessageKind.PlayerDataRejected))
                        return false;

                    lobbyId = reader.ReadUInt64();
                    epoch = reader.ReadString();
                    if (Encoding.UTF8.GetByteCount(epoch) > 64)
                        return false;

                    var payloadLength = reader.ReadInt32();
                    if ((payloadLength < 0) || (payloadLength > MessageByteCountMax) || (payloadLength != stream.Length - stream.Position))
                        return false;

                    payload = reader.ReadBytes(payloadLength);
                    return payload.Length == payloadLength;
                }
            }
            catch
            {
                return false;
            }
        }

        private static T DeserializePayload<T>(byte[] payload) where T : class
        {
            using (var stream = new MemoryStream(payload, false))
                return SteamPayloadDeserializer.Deserialize<T>(stream, 256, 16, 1024, 1024, 32 * 1024);
        }

        private List<RosterPlayerData> BuildLogicalRoster(bool includeMemberFields)
        {
            var roster = new List<RosterPlayerData>();
            foreach (var playerId in GetOrderedAcceptedPlayerIds())
            {
                MyNetInterface.Field[] fields;
                string nickname;
                if (playerId == _localSteamId)
                {
                    fields = _localPlayerFields;
                    nickname = _localPlayerNickname;
                }
                else if (LogicalPlayers.TryGetValue(playerId, out var player))
                {
                    fields = player.Fields;
                    nickname = player.Nickname;
                }
                else
                {
                    throw new FormatException("Steam logical player data is missing.");
                }

                roster.Add(new RosterPlayerData()
                {
                    Fields = includeMemberFields ? SelectNonPrivateFields(fields) : SelectFields(fields, MyNetInterface.Field.VisibilityEnum.Public),
                    Id = playerId,
                    Nickname = nickname,
                });
            }

            return roster;
        }

        private byte[] EncodeMemberSnapshot()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    var memberRoomFields = _state == StateEnum.Host ? SelectFields(_roomFields, MyNetInterface.Field.VisibilityEnum.Member) : _memberRoomFields;
                    writer.Write(EncodeFields(memberRoomFields));
                    writer.Write(EncodeRoster(BuildLogicalRoster(true)));
                }

                if (stream.Length > MessageByteCountMax - 128)
                    throw new FormatException("Steam member snapshot exceeds the message size limit.");

                return stream.ToArray();
            }
        }

        private void ApplyMemberSnapshot(byte[] payload)
        {
            MyNetInterface.Field[] memberRoomFields;
            List<RosterPlayerData> roster;
            using (var stream = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                memberRoomFields = DecodeFields(reader.ReadString());
                roster = DecodeRoster(reader.ReadString());
                if (stream.Position != stream.Length)
                    throw new FormatException("Steam member snapshot has trailing data.");
            }

            EnsureNoPrivateFields(memberRoomFields);
            memberRoomFields = SelectFields(memberRoomFields, MyNetInterface.Field.VisibilityEnum.Member);
            var playerIds = new HashSet<ulong>();
            var logicalPlayers = new Dictionary<ulong, RosterPlayerData>();
            foreach (var player in roster)
            {
                EnsureNoPrivateFields(player.Fields);
                player.Fields = SelectNonPrivateFields(player.Fields);
                if (playerIds.Add(player.Id) == false)
                    throw new FormatException("Steam member snapshot contains duplicate players.");

                if (player.Id != _localSteamId)
                    logicalPlayers.Add(player.Id, player);
            }

            if ((playerIds.Contains(_originalHostId) == false) || (playerIds.Contains(_localSteamId) == false))
                throw new FormatException("Steam member snapshot is missing a required player.");

            _memberRoomFields = memberRoomFields;
            AcceptedPlayerIds.Clear();
            foreach (var playerId in playerIds)
                AcceptedPlayerIds.Add(playerId);

            LogicalPlayers.Clear();
            foreach (var pair in logicalPlayers)
                LogicalPlayers.Add(pair.Key, pair.Value);
        }

        private static byte[] EncodeAdmissionRequest(string password, string nickname, MyNetInterface.Field[] fields)
        {
            EnsureStringByteCount(password, 1024, "room password");
            return EncodePlayerData(nickname, fields, password);
        }

        private static RosterPlayerData DecodeAdmissionRequest(ulong playerId, byte[] payload, out string password)
        {
            return DecodePlayerData(playerId, payload, true, out password);
        }

        private static byte[] EncodePlayerData(string nickname, MyNetInterface.Field[] fields)
        {
            return EncodePlayerData(nickname, fields, null);
        }

        private static byte[] EncodePlayerUpdate(ulong updateId, string nickname, MyNetInterface.Field[] fields)
        {
            var playerData = EncodePlayerData(nickname, fields);
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(updateId);
                    writer.Write(playerData.Length);
                    writer.Write(playerData);
                }

                return stream.ToArray();
            }
        }

        private static byte[] EncodePlayerUpdated(ulong playerId, byte[] snapshot)
        {
            if (playerId == 0)
                throw new FormatException("Steam updated player ID is invalid.");

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(playerId);
                    writer.Write(snapshot.Length);
                    writer.Write(snapshot);
                }

                return stream.ToArray();
            }
        }

        private static byte[] EncodePlayerData(string nickname, MyNetInterface.Field[] fields, string password)
        {
            nickname ??= string.Empty;
            EnsureStringByteCount(nickname, FieldValueByteCountMax, "player nickname");
            EnsureNoPrivateFields(fields);
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    if (password != null)
                        writer.Write(password);

                    writer.Write(nickname);
                    writer.Write(EncodeFields(fields));
                }

                if (stream.Length > MessageByteCountMax - 128)
                    throw new FormatException("Steam player data exceeds the message size limit.");

                return stream.ToArray();
            }
        }

        private static RosterPlayerData DecodePlayerData(ulong playerId, byte[] payload)
        {
            return DecodePlayerData(playerId, payload, false, out _);
        }

        private static RosterPlayerData DecodePlayerUpdate(ulong playerId, byte[] payload, out ulong updateId)
        {
            using (var stream = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                updateId = reader.ReadUInt64();
                if (updateId == 0)
                    throw new FormatException("Steam player update ID is invalid.");

                var length = reader.ReadInt32();
                if ((length < 0) || (length > MessageByteCountMax) || (length != stream.Length - stream.Position))
                    throw new FormatException("Steam player update payload length is invalid.");

                var playerData = reader.ReadBytes(length);
                if (playerData.Length != length)
                    throw new EndOfStreamException("Steam player update payload is truncated.");

                return DecodePlayerData(playerId, playerData);
            }
        }

        private static ulong DecodePlayerUpdated(byte[] payload, out byte[] snapshot)
        {
            using (var stream = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                var playerId = reader.ReadUInt64();
                if (playerId == 0)
                    throw new FormatException("Steam updated player ID is invalid.");

                var length = reader.ReadInt32();
                if ((length < 0) || (length > MessageByteCountMax) || (length != stream.Length - stream.Position))
                    throw new FormatException("Steam player snapshot payload length is invalid.");

                snapshot = reader.ReadBytes(length);
                if (snapshot.Length != length)
                    throw new EndOfStreamException("Steam player snapshot payload is truncated.");

                return playerId;
            }
        }

        private static RosterPlayerData DecodePlayerData(ulong playerId, byte[] payload, bool hasPassword, out string password)
        {
            using (var stream = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                password = hasPassword ? reader.ReadString() : null;
                if (hasPassword)
                    EnsureStringByteCount(password, 1024, "room password");

                var nickname = reader.ReadString();
                EnsureStringByteCount(nickname, FieldValueByteCountMax, "player nickname");
                var fields = DecodeFields(reader.ReadString());
                EnsureNoPrivateFields(fields);
                if (stream.Position != stream.Length)
                    throw new FormatException("Steam player data has trailing data.");

                return new RosterPlayerData()
                {
                    Fields = SelectNonPrivateFields(fields),
                    Id = playerId,
                    Nickname = nickname,
                };
            }
        }

        private static void EnsureNoPrivateFields(IEnumerable<MyNetInterface.Field> fields)
        {
            foreach (var field in fields)
            {
                if (field.visibility == MyNetInterface.Field.VisibilityEnum.Private)
                    throw new FormatException("Steam remote player data contains a private field.");
            }
        }

        private bool IsRawLobbyMember(ulong playerId)
        {
            var memberCount = SteamMatchmaking.GetNumLobbyMembers(_currentLobby);
            for (var index = 0; index < memberCount; ++index)
            {
                if (SteamMatchmaking.GetLobbyMemberByIndex(_currentLobby, index).m_SteamID == playerId)
                    return true;
            }

            return false;
        }

        private void ClosePeer(ulong playerId)
        {
            if ((playerId == 0) || (playerId == _localSteamId))
                return;

            var identity = default(SteamNetworkingIdentity);
            identity.SetSteamID(new CSteamID(playerId));
            SteamNetworkingMessages.CloseChannelWithUser(ref identity, MessageChannel);
        }

        private List<ulong> GetOrderedAcceptedPlayerIds()
        {
            var playerIds = new List<ulong>(AcceptedPlayerIds);
            playerIds.Sort();
            if (playerIds.Remove(_originalHostId))
                playerIds.Insert(0, _originalHostId);

            return playerIds;
        }

        private static string GetNickname(string configuredNickname, CSteamID steamId)
        {
            if (string.IsNullOrWhiteSpace(configuredNickname) == false)
            {
                EnsureStringByteCount(configuredNickname, FieldValueByteCountMax, "player nickname");
                return configuredNickname;
            }

            var nickname = steamId == SteamUser.GetSteamID() ? SteamFriends.GetPersonaName() : SteamFriends.GetFriendPersonaName(steamId);
            nickname ??= string.Empty;
            EnsureStringByteCount(nickname, FieldValueByteCountMax, "player nickname");
            return nickname;
        }

        private static MyNetInterface.Field[] NormalizeFields(IEnumerable<MyNetInterface.Field> fields)
        {
            if (fields == null)
                return Array.Empty<MyNetInterface.Field>();

            var result = new List<MyNetInterface.Field>();
            var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                var key = field.key ?? string.Empty;
                var value = field.value ?? string.Empty;
                EnsureStringByteCount(key, FieldKeyByteCountMax, "field key");
                EnsureStringByteCount(value, FieldValueByteCountMax, "field value");
                if (Enum.IsDefined(typeof(MyNetInterface.Field.VisibilityEnum), field.visibility) == false)
                    throw new FormatException("Steam field visibility is invalid.");

                var normalized = new MyNetInterface.Field()
                {
                    key = key,
                    value = value,
                    visibility = field.visibility,
                };
                if (indexes.TryGetValue(key, out var index))
                    result[index] = normalized;
                else
                {
                    if (result.Count >= FieldCountMax)
                        throw new FormatException($"Steam fields exceed the configured count limit {FieldCountMax}.");

                    indexes.Add(key, result.Count);
                    result.Add(normalized);
                }
            }

            return result.ToArray();
        }

        private static MyNetInterface.Field[] MergeFields(IEnumerable<MyNetInterface.Field> original, IEnumerable<MyNetInterface.Field> changes)
        {
            var result = new List<MyNetInterface.Field>(NormalizeFields(original));
            var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < result.Count; ++index)
                indexes[result[index].key] = index;

            foreach (var field in NormalizeFields(changes))
            {
                if (indexes.TryGetValue(field.key, out var index))
                    result[index] = field;
                else
                {
                    indexes.Add(field.key, result.Count);
                    result.Add(field);
                }
            }

            if (result.Count > FieldCountMax)
                throw new FormatException($"Steam fields exceed the configured count limit {FieldCountMax}.");

            return result.ToArray();
        }

        private static MyNetInterface.Field[] CloneFields(IEnumerable<MyNetInterface.Field> fields)
        {
            return NormalizeFields(fields);
        }

        private static MyNetInterface.Field[] SelectFields(IEnumerable<MyNetInterface.Field> fields, MyNetInterface.Field.VisibilityEnum visibility)
        {
            var result = new List<MyNetInterface.Field>();
            foreach (var field in NormalizeFields(fields))
            {
                if (field.visibility == visibility)
                    result.Add(field);
            }

            return result.ToArray();
        }

        private static MyNetInterface.Field[] SelectNonPrivateFields(IEnumerable<MyNetInterface.Field> fields)
        {
            var result = new List<MyNetInterface.Field>();
            foreach (var field in NormalizeFields(fields))
            {
                if (field.visibility != MyNetInterface.Field.VisibilityEnum.Private)
                    result.Add(field);
            }

            return result.ToArray();
        }

        private static string EncodeFields(MyNetInterface.Field[] fields)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(fields.Length);
                    foreach (var field in fields)
                    {
                        writer.Write(field.key);
                        writer.Write(field.value);
                        writer.Write((int)field.visibility);
                    }
                }

                var encoded = Convert.ToBase64String(stream.ToArray());
                EnsureStringByteCount(encoded, MetadataValueByteCountMax, "field metadata");
                return encoded;
            }
        }

        private static MyNetInterface.Field[] DecodeFields(string encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return Array.Empty<MyNetInterface.Field>();

            EnsureStringByteCount(encoded, MetadataValueByteCountMax, "field metadata");
            var data = Convert.FromBase64String(encoded);
            using (var stream = new MemoryStream(data, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                var count = reader.ReadInt32();
                if ((count < 0) || (count > FieldCountMax))
                    throw new FormatException("Steam field count is invalid.");

                var fields = new MyNetInterface.Field[count];
                for (var index = 0; index < count; ++index)
                {
                    var key = reader.ReadString();
                    var value = reader.ReadString();
                    var visibility = (MyNetInterface.Field.VisibilityEnum)reader.ReadInt32();
                    EnsureStringByteCount(key, FieldKeyByteCountMax, "field key");
                    EnsureStringByteCount(value, FieldValueByteCountMax, "field value");
                    if (Enum.IsDefined(typeof(MyNetInterface.Field.VisibilityEnum), visibility) == false)
                        throw new FormatException("Steam field visibility is invalid.");

                    fields[index] = new MyNetInterface.Field()
                    {
                        key = key,
                        value = value,
                        visibility = visibility,
                    };
                }

                if (stream.Position != stream.Length)
                    throw new FormatException("Steam field metadata has trailing data.");

                return NormalizeFields(fields);
            }
        }

        private static string EncodeRoster(List<RosterPlayerData> roster)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(roster.Count);
                    foreach (var player in roster)
                    {
                        writer.Write(player.Id);
                        writer.Write(player.Nickname ?? string.Empty);
                        writer.Write(EncodeFields(player.Fields));
                    }
                }

                return Convert.ToBase64String(stream.ToArray());
            }
        }

        private static List<RosterPlayerData> DecodeRoster(string encoded)
        {
            if (encoded.Length > RosterChunkCharacterCount * RosterChunkCountMax)
                throw new FormatException("Steam roster metadata is too large.");

            var data = Convert.FromBase64String(encoded);
            using (var stream = new MemoryStream(data, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                var count = reader.ReadInt32();
                if ((count <= 0) || (count > PlayerCountMax))
                    throw new FormatException("Steam roster count is invalid.");

                var roster = new List<RosterPlayerData>(count);
                var playerIds = new HashSet<ulong>();
                for (var index = 0; index < count; ++index)
                {
                    var playerId = reader.ReadUInt64();
                    var steamId = new CSteamID(playerId);
                    var nickname = reader.ReadString();
                    EnsureStringByteCount(nickname, FieldValueByteCountMax, "player nickname");
                    if ((steamId.IsValid() == false) || (steamId.BIndividualAccount() == false) || (playerIds.Add(playerId) == false))
                        throw new FormatException("Steam roster contains duplicate players.");

                    roster.Add(new RosterPlayerData()
                    {
                        Fields = DecodeFields(reader.ReadString()),
                        Id = playerId,
                        Nickname = nickname,
                    });
                }

                if (stream.Position != stream.Length)
                    throw new FormatException("Steam roster metadata has trailing data.");

                return roster;
            }
        }

        private static List<RosterPlayerData> ReadRoster(CSteamID lobby)
        {
            var revision = SteamMatchmaking.GetLobbyData(lobby, MetadataRosterRevision);
            var chunkCount = ParseBoundedInt(SteamMatchmaking.GetLobbyData(lobby, MetadataRosterChunkCount), 1, RosterChunkCountMax);
            if (string.IsNullOrWhiteSpace(revision))
                throw new FormatException("Steam roster revision is missing.");

            var builder = new StringBuilder();
            for (var index = 0; index < chunkCount; ++index)
            {
                var chunk = SteamMatchmaking.GetLobbyData(lobby, MetadataRosterChunkPrefix + index);
                var prefix = revision + ":";
                if ((chunk == null) || (chunk.StartsWith(prefix, StringComparison.Ordinal) == false))
                    throw new FormatException("Steam roster metadata is incomplete.");

                builder.Append(chunk, prefix.Length, chunk.Length - prefix.Length);
            }

            return DecodeRoster(builder.ToString());
        }

        private static byte[] EncodeUInt64(ulong value)
        {
            return BitConverter.GetBytes(value);
        }

        private static ulong DecodeUInt64(byte[] payload)
        {
            if ((payload == null) || (payload.Length != sizeof(ulong)))
                throw new FormatException("Steam control payload is invalid.");

            return BitConverter.ToUInt64(payload, 0);
        }

        private static void EnsureStringByteCount(string value, int maxByteCount, string description)
        {
            if (value == null)
                throw new FormatException($"Steam {description} is null.");

            if (Encoding.UTF8.GetByteCount(value) > maxByteCount)
                throw new FormatException($"Steam {description} exceeds {maxByteCount} UTF-8 bytes.");
        }

        private void SetLobbyData(string key, string value)
        {
            EnsureStringByteCount(value, MetadataValueByteCountMax, "lobby metadata value");
            if (SteamMatchmaking.SetLobbyData(_currentLobby, key, value) == false)
                throw new InvalidOperationException($"Steam rejected lobby metadata '{key}'.");
        }

        private void SetLobbyMemberData(string key, string value)
        {
            EnsureStringByteCount(value, MetadataValueByteCountMax, "member metadata value");
            SteamMatchmaking.SetLobbyMemberData(_currentLobby, key, value);
        }

        private static bool ReadBooleanLobbyData(CSteamID lobby, string key)
        {
            return SteamMatchmaking.GetLobbyData(lobby, key) == "1";
        }

        private static int ParseBoundedInt(string value, int minimum, int maximum)
        {
            if ((int.TryParse(value, out var result) == false) || (result < minimum) || (result > maximum))
                throw new FormatException("Steam numeric metadata is invalid.");

            return result;
        }

        private static string EncodeCode(ulong value)
        {
            var characters = new char[13];
            for (var index = characters.Length - 1; index >= 0; --index)
            {
                characters[index] = CodeAlphabet[(int)(value & 31)];
                value >>= 5;
            }

            return new string(characters);
        }

        private static bool TryDecodeCode(string code, out ulong value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(code) || (code.Length != 13))
                return false;

            foreach (var rawCharacter in code)
            {
                var character = char.ToUpperInvariant(rawCharacter);
                var digit = CodeAlphabet.IndexOf(character);
                if (digit < 0)
                    return false;

                if (value > (ulong.MaxValue - (ulong)digit) / 32)
                    return false;

                value = value * 32 + (ulong)digit;
            }

            return true;
        }
    }

    internal sealed class SteamNetHostService : MyNetHostServiceInterface
    {
        private readonly SteamNet Net;

        internal SteamNetHostService(SteamNet net)
        {
            Net = net;
        }

        void MyNetHostServiceInterface.Send(MyNetResponse response)
        {
            Net.QueueResponse(response);
        }
    }

    internal sealed class SteamNetMemberService : MyNetMemberServiceInterface
    {
        private readonly SteamNet Net;

        internal SteamNetMemberService(SteamNet net)
        {
            Net = net;
        }

        void MyNetMemberServiceInterface.Send(MyNetRequest request)
        {
            Net.QueueRequest(request);
        }
    }
}
#endif
