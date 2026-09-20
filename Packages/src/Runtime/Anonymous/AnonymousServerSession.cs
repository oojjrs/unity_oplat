namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousServerSession
    {
        internal AnonymousServerSession(string account, uint appId, AnonymousTransport.MessageQueue messages, string nickname)
        {
            Account = account;
            AppId = appId;
            Messages = messages;
            Nickname = nickname;
        }

        internal string Account { get; }
        public uint AppId { get; }
        internal AnonymousTransport.MessageQueue Messages { get; }
        internal string Nickname { get; }
    }
}
