using System.Collections.Generic;

namespace oojjrs.oplat
{
    public interface MyNetRoomInterface
    {
        public enum VisibilityEnum
        {
            Public = 0,
            FriendsOnly = 1,
            Private = 2,
        }

        string Code { get; }
        bool HasPassword { get; }
        MyNetPlayerInterface Host { get; }
        string HostId { get; }
        string Id { get; }
        bool IsLocked { get; }
        int PlayerCount { get; }
        int PlayerCountAvailable { get; }
        int PlayerCountMax { get; }
        IEnumerable<MyNetPlayerInterface> Players { get; }
        string Title { get; }
        VisibilityEnum Visibility { get; }

        string GetData(string key);
    }
}
