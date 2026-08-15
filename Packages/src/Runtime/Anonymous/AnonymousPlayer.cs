namespace oojjrs.oplat.anonymous
{
    internal class AnonymousPlayer : MyNetPlayerInterface
    {
        string MyNetPlayerInterface.Id => throw new System.NotImplementedException();
        bool MyNetPlayerInterface.IsHost => throw new System.NotImplementedException();
        string MyNetPlayerInterface.Nickname => throw new System.NotImplementedException();

        string MyNetPlayerInterface.GetData(string key)
        {
            throw new System.NotImplementedException();
        }
    }
}
