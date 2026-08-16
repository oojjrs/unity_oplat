namespace oojjrs.oplat.steam
{
    internal class SteamNet : MyNetInterface
    {
        MyNetHostServiceInterface MyNetInterface.Host => throw new System.NotImplementedException();
        MyNetLobbyServiceInterface MyNetInterface.Lobby { get; } = new SteamNetLobbyService();
        MyNetMemberServiceInterface MyNetInterface.Member => throw new System.NotImplementedException();
        MyNetPlayerServiceInterface MyNetInterface.Player { get; } = new SteamNetPlayerService();
        MyNetRoomServiceInterface MyNetInterface.Room { get; } = new SteamNetRoomService();
    }
}
