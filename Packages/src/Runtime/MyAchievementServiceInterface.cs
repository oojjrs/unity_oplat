using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat
{
    public interface MyAchievementServiceInterface
    {
        Task EnsureAsync(string definitionsJson, CancellationToken cancellationToken);
        Task<MyProgressDefinitions.Achievement> GetDefinitionAsync(string key, string language, CancellationToken cancellationToken);
        Task<MyProgressDefinitions.Achievement[]> GetDefinitionsAsync(string language, bool includeHidden, CancellationToken cancellationToken);
        Task<bool> IsUnlockedAsync(string key, CancellationToken cancellationToken);
        Task ResetAsync(CancellationToken cancellationToken);
        Task ResetAsync(string key, CancellationToken cancellationToken);
        Task UnlockAsync(string key, CancellationToken cancellationToken);
    }
}
