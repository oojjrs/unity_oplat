namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNet : MyNetInterface
    {
        MyNetLobbyServiceInterface MyNetInterface.Lobby { get; } = new AnonymousNetLobbyService();
        MyNetPlayerServiceInterface MyNetInterface.Player { get; } = new AnonymousNetPlayerService();
        MyNetRoomServiceInterface MyNetInterface.Room { get; } = new AnonymousNetRoomService();
    }
}
