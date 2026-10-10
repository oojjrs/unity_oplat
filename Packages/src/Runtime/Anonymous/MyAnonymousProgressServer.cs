using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    public static class MyAnonymousProgressServer
    {
        private static readonly Dictionary<uint, SemaphoreSlim> _gates = new();
        private static readonly Dictionary<(uint AppId, string Account), MyProgressServices> _services = new();
        private static readonly object _servicesLock = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearServices()
        {
            lock (_servicesLock)
            {
                _gates.Clear();
                _services.Clear();
            }
        }

        public static MyProgressServices GetServices(uint appId, string account, MyProgressDefinitions.WriterEnum writer)
        {
            if (string.IsNullOrWhiteSpace(account))
                throw new ArgumentException("An account is required.", nameof(account));

            lock (_servicesLock)
            {
                var key = (appId, account);
                if (_services.TryGetValue(key, out var services) == false)
                {
                    if (_gates.TryGetValue(appId, out var gate) == false)
                    {
                        gate = new SemaphoreSlim(1, 1);
                        _gates.Add(appId, gate);
                    }

                    var userPath = Path.GetDirectoryName(AnonymousServer.GetFriendStoragePath(appId, account));
                    var appPath = Path.GetDirectoryName(Path.GetDirectoryName(userPath));
                    var definitionsPath = Path.Combine(appPath, "progress-definitions.json");
                    services = new MyProgressServices(cancellationToken => AnonymousServer.ReadProgressDataAsync(Path.Combine(userPath, "stats.bin"), cancellationToken), (data, cancellationToken) => AnonymousServer.WriteProgressDataAsync(Path.Combine(userPath, "stats.bin"), data, cancellationToken), cancellationToken => AnonymousServer.ReadProgressDataAsync(Path.Combine(userPath, "achievements.bin"), cancellationToken), (data, cancellationToken) => AnonymousServer.WriteProgressDataAsync(Path.Combine(userPath, "achievements.bin"), data, cancellationToken), cancellationToken => AnonymousServer.ReadProgressDataAsync(definitionsPath, cancellationToken), (data, cancellationToken) => AnonymousServer.WriteProgressDataAsync(definitionsPath, data, cancellationToken), gate, (statKey, cancellationToken) => ReadGlobalIntAsync(appPath, statKey, cancellationToken), (statKey, cancellationToken) => ReadGlobalFloatAsync(appPath, statKey, cancellationToken));
                    _services.Add(key, services);
                }

                return services.WithWriter(writer);
            }
        }

        private static IEnumerable<string> GetUserPaths(string appPath)
        {
            var usersPath = Path.Combine(appPath, "users");
            return Directory.Exists(usersPath) ? Directory.EnumerateDirectories(usersPath) : Array.Empty<string>();
        }

        private static async Task<double> ReadGlobalFloatAsync(string appPath, string key, CancellationToken cancellationToken)
        {
            var total = 0d;
            foreach (var userPath in GetUserPaths(appPath))
            {
                var path = Path.Combine(userPath, "stats.bin");
                if (File.Exists(path) == false)
                    continue;

                var value = await MyPlatform.ReadStoredStatAsync(token => AnonymousServer.ReadProgressDataAsync(path, token), key, false, cancellationToken);
                if (value.IsFound)
                    total += value.FloatValue;
            }

            return total;
        }

        private static async Task<long> ReadGlobalIntAsync(string appPath, string key, CancellationToken cancellationToken)
        {
            var total = 0L;
            foreach (var userPath in GetUserPaths(appPath))
            {
                var path = Path.Combine(userPath, "stats.bin");
                if (File.Exists(path) == false)
                    continue;

                var value = await MyPlatform.ReadStoredStatAsync(token => AnonymousServer.ReadProgressDataAsync(path, token), key, true, cancellationToken);
                if (value.IsFound)
                    total = checked(total + value.IntValue);
            }

            return total;
        }
    }
}
