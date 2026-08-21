using UnityEngine;

namespace oojjrs.oplat
{
    public interface MyPlatformServiceInterface
    {
        string Account { get; }
        bool IsAlive { get; }
        bool IsRestartRequired { get; }
        MyNetInterface Net { get; }
        string Nickname { get; }
        Sprite ProfileSprite { get; }
        MyStorageServiceInterface Storage { get; }
    }
}
