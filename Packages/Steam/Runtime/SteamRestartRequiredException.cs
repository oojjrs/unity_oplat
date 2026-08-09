using oojjrs.oplat;

namespace oojjrs.oplat.steam
{
    public sealed class SteamRestartRequiredException : PlatformException
    {
        public uint AppId { get; }

        public SteamRestartRequiredException(uint appId)
            : base($"Steam must restart the application for App ID {appId}.")
        {
            AppId = appId;
        }
    }
}
