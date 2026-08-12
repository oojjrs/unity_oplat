using UnityEngine;

namespace oojjrs.oplat
{
    public interface MyPlatformServiceInterface
    {
        string Account { get; }
        bool IsAlive { get; }
        bool IsRestartRequired { get; }
        string Nickname { get; }
        Sprite ProfileSprite { get; }
    }
}
