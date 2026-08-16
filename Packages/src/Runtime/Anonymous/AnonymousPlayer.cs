namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousPlayer : MyNetPlayerInterface
    {
        private readonly MyNetInterface.Field[] Fields;
        private readonly string Id;
        private readonly bool IsHost;
        private readonly string Nickname;

        internal AnonymousPlayer(MyNetInterface.Field[] fields, string id, bool isHost, string nickname)
        {
            Fields = fields;
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
}
