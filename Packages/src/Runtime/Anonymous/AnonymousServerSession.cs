namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousServerSession
    {
        internal AnonymousServerSession(string account, AnonymousTransport.MessageQueue messages, string nickname)
        {
            Account = account;
            Messages = messages;
            Nickname = nickname;
        }

        internal string Account { get; }
        internal AnonymousTransport.MessageQueue Messages { get; }
        internal string Nickname { get; }
    }
}
