using UnityEngine;

namespace oojjrs.oplat
{
    public interface MyPlatformServiceInterface
    {
        string Account { get; }
        bool IsAlive { get; }
        string Nickname { get; }
        Sprite ProfileImage { get; }
    }
}
