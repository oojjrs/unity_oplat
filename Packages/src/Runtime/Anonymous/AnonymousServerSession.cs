namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousServerSession
    {
        internal AnonymousServerSession(string account, string nickname)
        {
            Account = account;
            Nickname = nickname;
        }

        internal string Account { get; }
        internal string Nickname { get; }
    }
}
