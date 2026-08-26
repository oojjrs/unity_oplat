using UnityEngine;

namespace oojjrs.oplat.ugsymous
{
    internal static class UgsymousRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            MyPlatform.Register(MyPlatformTypeEnum.Ugsymous, MyPlatform.CreateComponent<UgsymousPlatform>);
        }
    }
}
