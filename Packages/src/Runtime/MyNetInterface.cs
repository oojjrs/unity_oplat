namespace oojjrs.oplat
{
    public interface MyNetInterface
    {
        public interface CatchInterface
        {
            public enum FailureEnum
            {
                EmptyCode,
                EmptyMessage,
                EmptyPlayerId,
                EmptyRoomId,
                MessageTooLong,
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

        MyNetChatServiceInterface Chat { get; }
        MyNetHostServiceInterface Host { get; }
        MyNetLobbyServiceInterface Lobby { get; }
        MyNetMemberServiceInterface Member { get; }
        MyNetPlayerServiceInterface Player { get; }
        MyNetRoomServiceInterface Room { get; }
        bool UseLocal { get; set; }
    }
}
