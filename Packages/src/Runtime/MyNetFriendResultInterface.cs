namespace oojjrs.oplat
{
    public interface MyNetFriendResultInterface
    {
        void OnInvited(string playerId, string roomId);
        void OnJoinRequested(string playerId, string roomId);
    }
}
