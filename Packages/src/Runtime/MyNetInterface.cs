namespace oojjrs.oplat
{
    public interface MyNetInterface
    {
        public interface CatchInterface
        {
            public enum FailureEnum
            {
                EmptyCode,
                EmptyPlayerId,
                EmptyRoomId,
                NotFoundRoom,
                NotPermitted
            }

            void OnBusy();
            void OnException(MyNetSessionException e);
            void OnFailed(FailureEnum e);
        }

        public struct Field
        {
            public enum VisibilityEnum
            {
                Public,
                Member,
                Private,
            }

            public string key;
            public string value;
            public VisibilityEnum visibility;
        }

        MyNetLobbyServiceInterface Lobby { get; }
        MyNetPlayerServiceInterface Player { get; }
        MyNetRoomServiceInterface Room { get; }
    }
}
