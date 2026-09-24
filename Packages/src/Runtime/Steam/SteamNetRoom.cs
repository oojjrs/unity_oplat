using System.Collections.Generic;

namespace oojjrs.oplat.steam
{
#if STEAMWORKS_NET
    internal sealed class SteamNetRoom : MyNetRoomInterface
    {
        private readonly string Code;
        private readonly MyNetInterface.Field[] Fields;
        private readonly bool HasPassword;
        private readonly MyNetPlayerInterface Host;
        private readonly string HostId;
        private readonly string Id;
        private readonly bool IsLocked;
        private readonly int PlayerCountMax;
        private readonly IEnumerable<MyNetPlayerInterface> PlayerView;
        private readonly MyNetPlayerInterface[] Players;
        private readonly string Title;
        private readonly MyNetRoomInterface.VisibilityEnum Visibility;

        internal SteamNetRoom(string code, MyNetInterface.Field[] fields, bool hasPassword, string hostId, string id, bool isLocked, int playerCountMax, MyNetPlayerInterface[] players, string title, MyNetRoomInterface.VisibilityEnum visibility)
        {
            Code = code;
            Fields = (MyNetInterface.Field[])fields.Clone();
            HasPassword = hasPassword;
            HostId = hostId;
            Id = id;
            IsLocked = isLocked;
            PlayerCountMax = playerCountMax;
            Players = (MyNetPlayerInterface[])players.Clone();
            PlayerView = System.Array.AsReadOnly(Players);
            Title = title;
            Visibility = visibility;

            foreach (var player in Players)
            {
                if (player.Id == HostId)
                {
                    Host = player;
                    break;
                }
            }

            if (Host == null)
                throw new System.FormatException("Steam room host is missing from its players.");
        }

        string MyNetRoomInterface.Code => Code;
        bool MyNetRoomInterface.HasPassword => HasPassword;
        MyNetPlayerInterface MyNetRoomInterface.Host => Host;
        string MyNetRoomInterface.HostId => HostId;
        string MyNetRoomInterface.Id => Id;
        bool MyNetRoomInterface.IsLocked => IsLocked;
        int MyNetRoomInterface.PlayerCount => Players.Length;
        int MyNetRoomInterface.PlayerCountAvailable => System.Math.Max(0, PlayerCountMax - Players.Length);
        int MyNetRoomInterface.PlayerCountMax => PlayerCountMax;
        IEnumerable<MyNetPlayerInterface> MyNetRoomInterface.Players => PlayerView;
        string MyNetRoomInterface.Title => Title;
        MyNetRoomInterface.VisibilityEnum MyNetRoomInterface.Visibility => Visibility;

        string MyNetRoomInterface.GetData(string key)
        {
            foreach (var field in Fields)
            {
                if (field.key == key)
                    return field.value;
            }

            return null;
        }
    }
#endif
}
