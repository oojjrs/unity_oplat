using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat
{
    public interface MyStatsServiceInterface
    {
        Task AddAsync(string key, float value, CancellationToken cancellationToken);
        Task AddAsync(string key, int value, CancellationToken cancellationToken);
        Task EnsureAsync(string definitionsJson, CancellationToken cancellationToken);
        Task EnsureAsync(string key, float defaultValue, CancellationToken cancellationToken);
        Task EnsureAsync(string key, int defaultValue, CancellationToken cancellationToken);
        Task EnsureAverageRateAsync(string key, float defaultValue, double windowSeconds, CancellationToken cancellationToken);
        Task<float> GetAverageRateAsync(string key, CancellationToken cancellationToken);
        Task<MyProgressDefinitions.Stat> GetDefinitionAsync(string key, string language, CancellationToken cancellationToken);
        Task<MyProgressDefinitions.Stat[]> GetDefinitionsAsync(string language, CancellationToken cancellationToken);
        Task<float> GetFloatAsync(string key, CancellationToken cancellationToken);
        Task<double> GetGlobalFloatAsync(string key, CancellationToken cancellationToken);
        Task<long> GetGlobalIntAsync(string key, CancellationToken cancellationToken);
        Task<int> GetIntAsync(string key, CancellationToken cancellationToken);
        Task ResetAsync(CancellationToken cancellationToken);
        Task ResetAsync(string key, CancellationToken cancellationToken);
        Task SetAsync(string key, float value, CancellationToken cancellationToken);
        Task SetAsync(string key, int value, CancellationToken cancellationToken);
        Task UpdateAverageRateAsync(string key, float count, double sessionLengthSeconds, CancellationToken cancellationToken);
    }
}
