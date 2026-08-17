namespace oojjrs.oplat.steam
{
#if STEAMWORKS_NET
    internal sealed class SteamNetPlayer : MyNetPlayerInterface
    {
        private readonly MyNetInterface.Field[] Fields;
        private readonly string Id;
        private readonly bool IsHost;
        private readonly string Nickname;

        internal SteamNetPlayer(MyNetInterface.Field[] fields, string id, bool isHost, string nickname)
        {
            Fields = (MyNetInterface.Field[])fields.Clone();
            Id = id;
            IsHost = isHost;
            Nickname = nickname;
        }

        string MyNetPlayerInterface.Id => Id;
        bool MyNetPlayerInterface.IsHost => IsHost;
        string MyNetPlayerInterface.Nickname => Nickname;

        string MyNetPlayerInterface.GetData(string key)
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
