using System.Collections.Generic;

namespace oojjrs.oplat.steam
{
    internal class SteamNetRoom : MyNetRoomInterface
    {
        string MyNetRoomInterface.Code => throw new System.NotImplementedException();
        bool MyNetRoomInterface.HasPassword => throw new System.NotImplementedException();
        MyNetPlayerInterface MyNetRoomInterface.Host => throw new System.NotImplementedException();
        string MyNetRoomInterface.HostId => throw new System.NotImplementedException();
        string MyNetRoomInterface.Id => throw new System.NotImplementedException();
        bool MyNetRoomInterface.IsLocked => throw new System.NotImplementedException();
        bool MyNetRoomInterface.IsPrivate => throw new System.NotImplementedException();
        int MyNetRoomInterface.PlayerCount => throw new System.NotImplementedException();
        int MyNetRoomInterface.PlayerCountAvailable => throw new System.NotImplementedException();
        int MyNetRoomInterface.PlayerCountMax => throw new System.NotImplementedException();
        IEnumerable<MyNetPlayerInterface> MyNetRoomInterface.Players => throw new System.NotImplementedException();
        string MyNetRoomInterface.Title => throw new System.NotImplementedException();

        string MyNetRoomInterface.GetData(string key)
        {
            throw new System.NotImplementedException();
        }
    }
}
