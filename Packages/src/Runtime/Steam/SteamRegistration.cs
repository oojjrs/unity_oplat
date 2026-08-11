#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
using UnityEngine;

namespace oojjrs.oplat.steam
{
    internal static class SteamRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            MyPlatform.Register(MyPlatformTypeEnum.Steam, MyPlatform.CreateComponent<SteamPlatform>);
        }
    }
}
#endif
