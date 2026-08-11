using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal static class AnonymousRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            MyPlatform.Register(MyPlatformTypeEnum.Anonymous, MyPlatform.CreateComponent<AnonymousPlatform>);
        }
    }
}
