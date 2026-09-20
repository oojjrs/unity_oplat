namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousServerSession
    {
        internal AnonymousServerSession(string account, uint appId, string baseAccount, AnonymousTransport.MessageQueue messages, string nickname, string projectKey)
        {
            Account = account;
            AppId = appId;
            BaseAccount = baseAccount;
            Messages = messages;
            Nickname = nickname;
            ProjectKey = projectKey;
        }

        internal string Account { get; }
        public uint AppId { get; }
        internal string BaseAccount { get; }
        internal AnonymousTransport.MessageQueue Messages { get; }
        internal string Nickname { get; }
        public string ProjectKey { get; }
    }
}
