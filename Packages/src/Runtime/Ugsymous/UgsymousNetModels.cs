using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Friends.Models;
using Unity.Services.Lobbies.Models;
using Unity.Services.Multiplayer;

namespace oojjrs.oplat.ugsymous
{
    [Serializable]
    internal sealed class UgsymousFriendActivity
    {
        public string RoomId = string.Empty;
    }

    [Serializable]
    internal sealed class UgsymousFriendInvitation
    {
        internal const string KindValue = "oplat-room-invite-v1";

        public string Kind = string.Empty;
        public string RoomId = string.Empty;
    }

    internal sealed class UgsymousFriend : MyNetFriendInterface
    {
        private readonly string _id;
        private readonly string _nickname;
        private readonly string _roomId;
        private readonly MyNetFriendInterface.StateEnum _state;

        string MyNetFriendInterface.Id => _id;
        string MyNetFriendInterface.Nickname => _nickname;
        string MyNetFriendInterface.RoomId => _roomId;
        MyNetFriendInterface.StateEnum MyNetFriendInterface.State => _state;

        internal UgsymousFriend(Relationship relationship)
        {
            var member = relationship.Member;
            _id = member?.Id ?? string.Empty;
            _nickname = string.IsNullOrWhiteSpace(member?.Profile?.Name) ? _id : member.Profile.Name;
            _state = ToState(member?.Presence?.Availability ?? Availability.Offline);
            _roomId = ReadRoomId(member?.Presence);
        }

        private static string ReadRoomId(Presence presence)
        {
            if ((presence == null) || ((presence.Availability != Availability.Online) && (presence.Availability != Availability.Busy) && (presence.Availability != Availability.Away)))
                return string.Empty;

            try
            {
                return presence.GetActivity<UgsymousFriendActivity>()?.RoomId ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static MyNetFriendInterface.StateEnum ToState(Availability availability) => availability switch
        {
            Availability.Away => MyNetFriendInterface.StateEnum.Away,
            Availability.Busy => MyNetFriendInterface.StateEnum.Busy,
            Availability.Invisible => MyNetFriendInterface.StateEnum.Invisible,
            Availability.Online => MyNetFriendInterface.StateEnum.Online,
            _ => MyNetFriendInterface.StateEnum.Offline,
        };
    }

    internal sealed class UgsymousPlayer : MyNetPlayerInterface
    {
        private readonly UgsymousRoom _room;

        string MyNetPlayerInterface.Id => Player.Id;
        bool MyNetPlayerInterface.IsHost => ((MyNetRoomInterface)_room).HostId == Player.Id;
        string MyNetPlayerInterface.Nickname => ((MyNetPlayerInterface)this).GetData(UgsymousNet.PlayerPropertyNickname);
        internal IReadOnlyPlayer Player { get; set; }

        internal UgsymousPlayer(UgsymousRoom room, IReadOnlyPlayer player)
        {
            _room = room;
            Player = player;
        }

        string MyNetPlayerInterface.GetData(string key) => Player.Properties != null && Player.Properties.TryGetValue(key, out var value) ? value.Value : string.Empty;
    }

    internal sealed class UgsymousRoom : MyNetRoomInterface
    {
        private readonly Dictionary<string, UgsymousPlayer> _players = new();

        string MyNetRoomInterface.Code => Session.Code;
        bool MyNetRoomInterface.HasPassword => Session.HasPassword;
        MyNetPlayerInterface MyNetRoomInterface.Host => ((MyNetRoomInterface)this).Players.FirstOrDefault(t => t.IsHost);
        string MyNetRoomInterface.HostId => Session.Host;
        string MyNetRoomInterface.Id => Session.Id;
        bool MyNetRoomInterface.IsLocked => Session.IsLocked;
        bool MyNetRoomInterface.IsPrivate => Session.IsPrivate;
        int MyNetRoomInterface.PlayerCount => Session.PlayerCount;
        int MyNetRoomInterface.PlayerCountAvailable => Session.AvailableSlots;
        int MyNetRoomInterface.PlayerCountMax => Session.MaxPlayers;
        IEnumerable<MyNetPlayerInterface> MyNetRoomInterface.Players => Session.Players.Select(GetPlayer);
        string MyNetRoomInterface.Title => Session.Name;
        internal ISession Session { get; set; }

        internal UgsymousRoom(ISession session) => Session = session;

        string MyNetRoomInterface.GetData(string key) => Session.Properties != null && Session.Properties.TryGetValue(key, out var value) ? value.Value : string.Empty;

        private MyNetPlayerInterface GetPlayer(IReadOnlyPlayer player)
        {
            if (_players.TryGetValue(player.Id, out var value))
                value.Player = player;
            else
                _players.Add(player.Id, value = new(this, player));

            return value;
        }
    }

    internal sealed class UgsymousRoomStub : MyNetRoomInterface
    {
        string MyNetRoomInterface.Code => string.Empty;
        bool MyNetRoomInterface.HasPassword => Session.HasPassword;
        MyNetPlayerInterface MyNetRoomInterface.Host => null;
        string MyNetRoomInterface.HostId => Session.HostId;
        string MyNetRoomInterface.Id => Session.Id;
        bool MyNetRoomInterface.IsLocked => Session.IsLocked;
        bool MyNetRoomInterface.IsPrivate => false;
        int MyNetRoomInterface.PlayerCount => Session.MaxPlayers - Session.AvailableSlots;
        int MyNetRoomInterface.PlayerCountAvailable => Session.AvailableSlots;
        int MyNetRoomInterface.PlayerCountMax => Session.MaxPlayers;
        IEnumerable<MyNetPlayerInterface> MyNetRoomInterface.Players => Enumerable.Empty<MyNetPlayerInterface>();
        string MyNetRoomInterface.Title => Session.Name;
        internal Lobby Session { get; set; }

        internal UgsymousRoomStub(Lobby session) => Session = session;
        string MyNetRoomInterface.GetData(string key) => Session.Data != null && Session.Data.TryGetValue(key, out var value) ? value.Value : string.Empty;
    }
}
