namespace oojjrs.oplat
{
    public interface MyNetPlayerInterface
    {
        string Id { get; }
        bool IsHost { get; }
        string Nickname { get; }

        string GetData(string key);
    }
}
