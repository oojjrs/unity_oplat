namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNet : MyNetInterface
    {
        private readonly AnonymousNetLobbyService _lobby;
        private readonly AnonymousServer _server = new();

        internal AnonymousNet()
        {
            _lobby = new(_server);
        }

        MyNetLobbyServiceInterface MyNetInterface.Lobby => _lobby;
        MyNetPlayerServiceInterface MyNetInterface.Player { get; } = new AnonymousNetPlayerService();
        MyNetRoomServiceInterface MyNetInterface.Room { get; } = new AnonymousNetRoomService();

        internal void Shutdown()
        {
            _server.Shutdown();
        }
    }
}
