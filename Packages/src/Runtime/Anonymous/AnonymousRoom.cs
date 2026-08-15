using System.Collections.Generic;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousRoom : MyNetRoomInterface
    {
        private readonly string _code;
        private readonly MyNetInterface.Field[] _fields;
        private readonly bool _hasPassword;
        private readonly MyNetPlayerInterface _host;
        private readonly string _hostId;
        private readonly string _id;
        private readonly bool _isLocked;
        private readonly bool _isPrivate;
        private readonly int _playerCountMax;
        private readonly MyNetPlayerInterface[] _players;
        private readonly string _title;

        internal AnonymousRoom(string code, MyNetInterface.Field[] fields, bool hasPassword, string hostId, string id, bool isLocked, bool isPrivate, int playerCountMax, MyNetPlayerInterface[] players, string title)
        {
            _code = code;
            _fields = fields;
            _hasPassword = hasPassword;
            _hostId = hostId;
            _id = id;
            _isLocked = isLocked;
            _isPrivate = isPrivate;
            _playerCountMax = playerCountMax;
            _players = players;
            _title = title;

            foreach (var player in _players)
            {
                if (string.Equals(player.Id, _hostId, System.StringComparison.Ordinal))
                {
                    _host = player;
                    break;
                }
            }

            if (_host == null)
                throw new System.FormatException("Anonymous room host is missing from its players.");
        }

        string MyNetRoomInterface.Code => _code;
        bool MyNetRoomInterface.HasPassword => _hasPassword;
        MyNetPlayerInterface MyNetRoomInterface.Host => _host;
        string MyNetRoomInterface.HostId => _hostId;
        string MyNetRoomInterface.Id => _id;
        bool MyNetRoomInterface.IsLocked => _isLocked;
        bool MyNetRoomInterface.IsPrivate => _isPrivate;
        int MyNetRoomInterface.PlayerCount => _players.Length;
        int MyNetRoomInterface.PlayerCountAvailable => System.Math.Max(0, _playerCountMax - _players.Length);
        int MyNetRoomInterface.PlayerCountMax => _playerCountMax;
        IEnumerable<MyNetPlayerInterface> MyNetRoomInterface.Players => _players;
        string MyNetRoomInterface.Title => _title;

        string MyNetRoomInterface.GetData(string key)
        {
            foreach (var field in _fields)
            {
                if (string.Equals(field.key, key, System.StringComparison.Ordinal))
                    return field.value;
            }

            return null;
        }
    }
}
