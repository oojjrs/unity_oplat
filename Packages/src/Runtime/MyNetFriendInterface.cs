namespace oojjrs.oplat
{
    public interface MyNetFriendInterface
    {
        public enum StateEnum
        {
            Away = 3,
            Busy = 2,
            Invisible = 7,
            LookingToPlay = 6,
            LookingToTrade = 5,
            Offline = 0,
            Online = 1,
            Snooze = 4,
        }

        string Id { get; }
        string Nickname { get; }
        string RoomId { get; }
        StateEnum State { get; }
    }
}
