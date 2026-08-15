using System.Collections.Generic;

namespace oojjrs.oplat
{
    public interface MyNetRoomInterface
    {
        string Code { get; }
        bool HasPassword { get; }
        MyNetPlayerInterface Host { get; }
        string HostId { get; }
        string Id { get; }
        bool IsLocked { get; }
        bool IsPrivate { get; }
        int PlayerCount { get; }
        int PlayerCountAvailable { get; }
        int PlayerCountMax { get; }
        IEnumerable<MyNetPlayerInterface> Players { get; }
        string Title { get; }

        string GetData(string key);
    }
}
