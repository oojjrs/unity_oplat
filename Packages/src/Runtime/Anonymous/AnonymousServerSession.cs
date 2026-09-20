namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousServerSession
    {
        internal AnonymousServerSession(string account, uint appId, string instanceId, AnonymousTransport.MessageQueue messages, string nickname, string projectKey)
        {
            Account = account;
            AppId = appId;
            InstanceId = instanceId ?? string.Empty;
            Messages = messages;
            Nickname = nickname;
            ProjectKey = projectKey;
        }

        internal string Account { get; }
        public uint AppId { get; }
        internal string InstanceId { get; }
        internal AnonymousTransport.MessageQueue Messages { get; }
        internal string Nickname { get; }
        public string ProjectKey { get; }
    }
}
