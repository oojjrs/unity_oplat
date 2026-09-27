using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat
{
    public interface MyPlatformServiceInterface
    {
        string Account { get; }
        MyAchievementServiceInterface Achievements { get; }
        bool IsAlive { get; }
        bool IsRestartRequired { get; }
        MyNetInterface Net { get; }
        string Nickname { get; }
        Sprite ProfileSprite { get; }
        MyStatsServiceInterface Stats { get; }
        MyStorageServiceInterface Storage { get; }
        MyTimeServiceInterface Time { get; }

        Task ResetAllProgressAsync(CancellationToken cancellationToken);
    }
}
