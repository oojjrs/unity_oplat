namespace oojjrs.oplat.steam
{
    internal class SteamNet : MyNetInterface
    {
        MyNetLobbyServiceInterface MyNetInterface.Lobby { get; } = new SteamNetLobbyService();
        MyNetPlayerServiceInterface MyNetInterface.Player { get; } = new SteamNetPlayerService();
        MyNetRoomServiceInterface MyNetInterface.Room { get; } = new SteamNetRoomService();
    }
}
