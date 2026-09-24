using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Services.Multiplayer;

namespace oojjrs.oplat.ugsymous
{
    internal sealed class UgsymousNet : MyNetInterface, IDisposable
    {
        internal const string PlayerPropertyNickname = "__Nickname__";
        internal const string SessionPropertyVisibility = "__Visibility__";
        private readonly UgsymousNetChatService _chat;
        private readonly UgsymousNetFriendService _friend;
        private readonly UgsymousNetHostService _host;
        private readonly CancellationTokenSource _lifetimeSource = new();
        private readonly CancellationToken _lifetimeToken;
        private readonly UgsymousNetLobbyService _lobby;
        private readonly UgsymousNetMemberService _member;
        private readonly UgsymousNetPlayerService _player;
        private readonly UgsymousNetRoomService _room;
        private readonly UgsymousTransport _transport;
        private bool _useLocal;

        MyNetChatServiceInterface MyNetInterface.Chat => _chat;
        MyNetFriendServiceInterface MyNetInterface.Friend => _friend;
        MyNetHostServiceInterface MyNetInterface.Host => _host;
        MyNetLobbyServiceInterface MyNetInterface.Lobby => _lobby;
        MyNetMemberServiceInterface MyNetInterface.Member => _member;
        MyNetPlayerServiceInterface MyNetInterface.Player => _player;
        MyNetRoomServiceInterface MyNetInterface.Room => _room;
        bool MyNetInterface.UseLocal { get => _useLocal; set => _useLocal = value; }
        internal string Account { get; }
        internal MyNetChatResultInterface ChatResult { get; }
        internal MyNetFriendResultInterface FriendResult { get; }
        internal MyNetHostResultInterface HostResult { get; }
        internal CancellationToken LifetimeCancellationToken => _lifetimeToken;
        internal MyNetMemberResultInterface MemberResult { get; }
        internal MyNetPlayerServiceInterface.UpdateResultInterface PlayerResult { get; }
        internal MyNetRoomServiceInterface.UpdateResultInterface RoomResult { get; }
        internal MyTimeServiceInterface Time { get; }

        internal UgsymousNet(string account, MyPlatformInitializer.CallbackInterface callback, MyTimeServiceInterface time)
        {
            _lifetimeToken = _lifetimeSource.Token;
            Account = account;
            ChatResult = callback.ChatResult;
            FriendResult = callback.FriendResult;
            HostResult = callback.HostResult;
            MemberResult = callback.MemberResult;
            PlayerResult = callback.PlayerResult;
            RoomResult = callback.RoomResult;
            Time = time ?? throw new ArgumentNullException(nameof(time));
            _transport = new(account);
            _chat = new(this);
            _friend = new(this);
            _host = new(this);
            _lobby = new(this);
            _member = new(this);
            _player = new(this);
            _room = new(this);
        }

        internal static Dictionary<string, PlayerProperty> ToPlayerProperties(IEnumerable<MyNetInterface.Field> fields, string nickname = null)
        {
            var result = (fields ?? Enumerable.Empty<MyNetInterface.Field>()).ToDictionary(t => t.key, t => new PlayerProperty(t.value, ToVisibility(t.visibility)));
            if (string.IsNullOrWhiteSpace(nickname) == false)
                result[PlayerPropertyNickname] = new(nickname, VisibilityPropertyOptions.Public);

            return result;
        }

        internal CancellationTokenSource CreateCancellationSource(CancellationToken cancellationToken) => CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, LifetimeCancellationToken);

        internal static MyNetRoomInterface.VisibilityEnum GetVisibility(ISession session)
        {
            if (session.IsPrivate == false)
                return MyNetRoomInterface.VisibilityEnum.Public;

            if ((session.Properties != null) && session.Properties.TryGetValue(SessionPropertyVisibility, out var property) && Enum.TryParse(property.Value, out MyNetRoomInterface.VisibilityEnum visibility) && Enum.IsDefined(typeof(MyNetRoomInterface.VisibilityEnum), visibility))
                return visibility;

            return MyNetRoomInterface.VisibilityEnum.Private;
        }

        internal static Dictionary<string, SessionProperty> ToSessionProperties(IEnumerable<MyNetInterface.Field> fields, MyNetRoomInterface.VisibilityEnum visibility)
        {
            if (Enum.IsDefined(typeof(MyNetRoomInterface.VisibilityEnum), visibility) == false)
                throw new ArgumentOutOfRangeException(nameof(visibility));

            var result = (fields ?? Enumerable.Empty<MyNetInterface.Field>()).ToDictionary(t => t.key, t => new SessionProperty(t.value, ToVisibility(t.visibility)));
            result[SessionPropertyVisibility] = new SessionProperty(visibility.ToString(), VisibilityPropertyOptions.Member);
            return result;
        }

        internal static bool ToIsPrivate(MyNetRoomInterface.VisibilityEnum visibility)
        {
            if (Enum.IsDefined(typeof(MyNetRoomInterface.VisibilityEnum), visibility) == false)
                throw new ArgumentOutOfRangeException(nameof(visibility));

            return visibility != MyNetRoomInterface.VisibilityEnum.Public;
        }

        private static VisibilityPropertyOptions ToVisibility(MyNetInterface.Field.VisibilityEnum value) => value switch
        {
            MyNetInterface.Field.VisibilityEnum.Public => VisibilityPropertyOptions.Public,
            MyNetInterface.Field.VisibilityEnum.Member => VisibilityPropertyOptions.Member,
            MyNetInterface.Field.VisibilityEnum.Private => VisibilityPropertyOptions.Private,
            _ => VisibilityPropertyOptions.Public,
        };

        internal void Update()
        {
            _friend.Update();
            _transport.Update();
            while (_transport.TryReceiveResponse(out var response))
                _member.Receive(response);

            _member.HandleResponses();

            while (_transport.TryReceiveRequest(out var request))
                _host.Receive(request);

            while (_member.TryDequeue(out var request))
            {
                if (_useLocal || _transport.IsHost)
                    _host.Receive(request);
                else
                    _transport.SendRequest(request);
            }

            _host.HandleRequests();
            while (_host.TryDequeue(out var response))
            {
                _member.Receive(response);
                if (_useLocal == false)
                    _transport.SendResponse(response);
            }
        }

        internal UgsymousTransport Transport => _transport;
        public void Dispose()
        {
            _lifetimeSource.Cancel();
            _lobby.Stop();
            _room.Dispose();
            _friend.Dispose();
            _chat.Dispose();
            _transport.Dispose();
            _lifetimeSource.Dispose();
        }
    }

    internal sealed class UgsymousNetHostService : MyNetHostServiceInterface
    {
        private readonly UgsymousNet _net;
        private readonly HashQueue<MyNetRequest> _requests = new();
        private readonly HashQueue<MyNetResponse> _responses = new();
        internal UgsymousNetHostService(UgsymousNet net) => _net = net;
        internal void Receive(MyNetRequest request) => _requests.Enqueue(request);
        void MyNetHostServiceInterface.Send(MyNetResponse response) => _responses.Enqueue(response);
        internal bool TryDequeue(out MyNetResponse response) => _responses.TryDequeue(out response);
        internal void HandleRequests()
        {
            if (_requests.Count <= 0)
                return;

            while (_requests.TryDequeue(out var request))
                _net.HostResult.OnReceived(request);

            _net.HostResult.OnFinishThisHandling();
        }
    }

    internal sealed class UgsymousNetMemberService : MyNetMemberServiceInterface
    {
        private readonly UgsymousNet _net;
        private readonly HashQueue<MyNetRequest> _requests = new();
        private readonly HashQueue<MyNetResponse> _responses = new();
        internal UgsymousNetMemberService(UgsymousNet net) => _net = net;
        internal void Receive(MyNetResponse response) => _responses.Enqueue(response);
        void MyNetMemberServiceInterface.Send(MyNetRequest request) => _requests.Enqueue(request);
        internal bool TryDequeue(out MyNetRequest request) => _requests.TryDequeue(out request);
        internal void HandleResponses()
        {
            if (_responses.Count <= 0)
                return;

            while (_responses.TryDequeue(out var response))
                _net.MemberResult.OnReceived(response);

            _net.MemberResult.OnFinishThisHandling();
        }
    }
}
