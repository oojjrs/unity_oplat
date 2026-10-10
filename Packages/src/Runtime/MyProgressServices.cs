using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat
{
    public sealed class MyProgressServices
    {
        private sealed class ConfiguredMyAchievementService : MyAchievementServiceInterface
        {
            private readonly State _state;
            private readonly MyProgressDefinitions.WriterEnum _writer;

            public ConfiguredMyAchievementService(State state, MyProgressDefinitions.WriterEnum writer)
            {
                _state = state;
                _writer = writer;
            }

            Task MyAchievementServiceInterface.EnsureAsync(string definitionsJson, CancellationToken cancellationToken)
            {
                return _state.EnsureAsync(definitionsJson, false, cancellationToken);
            }

            Task<MyProgressDefinitions.Achievement> MyAchievementServiceInterface.GetDefinitionAsync(string key, string language, CancellationToken cancellationToken)
            {
                return _state.RunAsync(() => Task.FromResult(_state.GetAchievement(key).Localize(language)), cancellationToken);
            }

            Task<MyProgressDefinitions.Achievement[]> MyAchievementServiceInterface.GetDefinitionsAsync(string language, bool includeHidden, CancellationToken cancellationToken)
            {
                return _state.RunAsync(async () =>
                {
                    var result = new List<MyProgressDefinitions.Achievement>();
                    foreach (var definition in _state.Definitions.Achievements ?? Array.Empty<MyProgressDefinitions.Achievement>())
                    {
                        if (includeHidden || (definition.Hidden == false) || await _state.Achievements.IsUnlockedAsync(definition.Key, cancellationToken))
                            result.Add(definition.Localize(language));
                    }

                    return result.ToArray();
                }, cancellationToken);
            }

            Task<bool> MyAchievementServiceInterface.IsUnlockedAsync(string key, CancellationToken cancellationToken)
            {
                return _state.RunAsync(async () =>
                {
                    _state.GetAchievement(key);
                    return await _state.Achievements.IsUnlockedAsync(key, cancellationToken);
                }, cancellationToken);
            }

            Task MyAchievementServiceInterface.ResetAsync(CancellationToken cancellationToken)
            {
                return _state.RunAsync(async () =>
                {
                    foreach (var definition in _state.Definitions.Achievements ?? Array.Empty<MyProgressDefinitions.Achievement>())
                        MyProgressDefinitions.EnsureWriter(definition.SetBy, _writer);

                    await _state.Achievements.ResetAsync(cancellationToken);
                }, cancellationToken);
            }

            Task MyAchievementServiceInterface.ResetAsync(string key, CancellationToken cancellationToken)
            {
                return _state.RunAsync(async () =>
                {
                    MyProgressDefinitions.EnsureWriter(_state.GetAchievement(key).SetBy, _writer);
                    await _state.Achievements.ResetAsync(key, cancellationToken);
                }, cancellationToken);
            }

            Task MyAchievementServiceInterface.UnlockAsync(string key, CancellationToken cancellationToken)
            {
                return _state.RunAsync(async () =>
                {
                    MyProgressDefinitions.EnsureWriter(_state.GetAchievement(key).SetBy, _writer);
                    await _state.Achievements.UnlockAsync(key, cancellationToken);
                }, cancellationToken);
            }
        }

        private sealed class ConfiguredMyStatsService : MyStatsServiceInterface
        {
            private readonly State _state;
            private readonly MyProgressDefinitions.WriterEnum _writer;

            public ConfiguredMyStatsService(State state, MyProgressDefinitions.WriterEnum writer)
            {
                _state = state;
                _writer = writer;
            }

            Task MyStatsServiceInterface.AddAsync(string key, float value, CancellationToken cancellationToken)
            {
                return ChangeFloatAsync(key, value, true, cancellationToken);
            }

            Task MyStatsServiceInterface.AddAsync(string key, int value, CancellationToken cancellationToken)
            {
                return ChangeIntAsync(key, value, true, cancellationToken);
            }

            Task MyStatsServiceInterface.EnsureAsync(string definitionsJson, CancellationToken cancellationToken)
            {
                return _state.EnsureAsync(definitionsJson, true, cancellationToken);
            }

            Task MyStatsServiceInterface.EnsureAsync(string key, float defaultValue, CancellationToken cancellationToken)
            {
                return _state.EnsureAsync(new MyProgressDefinitions() { Stats = new[] { new MyProgressDefinitions.Stat() { Key = key, Type = "FLOAT", DefaultValue = defaultValue } } }.ToJson(), true, cancellationToken, true);
            }

            Task MyStatsServiceInterface.EnsureAsync(string key, int defaultValue, CancellationToken cancellationToken)
            {
                return _state.EnsureAsync(new MyProgressDefinitions() { Stats = new[] { new MyProgressDefinitions.Stat() { Key = key, Type = "INT", DefaultValue = defaultValue } } }.ToJson(), true, cancellationToken, true);
            }

            Task MyStatsServiceInterface.EnsureAverageRateAsync(string key, float defaultValue, double windowSeconds, CancellationToken cancellationToken)
            {
                return _state.EnsureAsync(new MyProgressDefinitions() { Stats = new[] { new MyProgressDefinitions.Stat() { Key = key, Type = "AVGRATE", DefaultValue = defaultValue, Window = windowSeconds, SecondsPerUnit = 1d } } }.ToJson(), true, cancellationToken, true);
            }

            Task<float> MyStatsServiceInterface.GetAverageRateAsync(string key, CancellationToken cancellationToken)
            {
                return _state.RunAsync(() => _state.Stats.GetAverageRateAsync(key, cancellationToken), cancellationToken);
            }

            Task<MyProgressDefinitions.Stat> MyStatsServiceInterface.GetDefinitionAsync(string key, string language, CancellationToken cancellationToken)
            {
                return _state.RunAsync(() => Task.FromResult(_state.GetStat(key).Localize(language)), cancellationToken);
            }

            Task<MyProgressDefinitions.Stat[]> MyStatsServiceInterface.GetDefinitionsAsync(string language, CancellationToken cancellationToken)
            {
                return _state.RunAsync(() => Task.FromResult((_state.Definitions.Stats ?? Array.Empty<MyProgressDefinitions.Stat>()).Select(definition => definition.Localize(language)).ToArray()), cancellationToken);
            }

            Task<float> MyStatsServiceInterface.GetFloatAsync(string key, CancellationToken cancellationToken)
            {
                return _state.RunAsync(() => _state.Stats.GetFloatAsync(key, cancellationToken), cancellationToken);
            }

            Task<double> MyStatsServiceInterface.GetGlobalFloatAsync(string key, CancellationToken cancellationToken)
            {
                return _state.RunAsync(() =>
                {
                    EnsureAggregated(key, "FLOAT");
                    return _state.ReadGlobalFloatAsync(key, cancellationToken);
                }, cancellationToken);
            }

            Task<long> MyStatsServiceInterface.GetGlobalIntAsync(string key, CancellationToken cancellationToken)
            {
                return _state.RunAsync(() =>
                {
                    EnsureAggregated(key, "INT");
                    return _state.ReadGlobalIntAsync(key, cancellationToken);
                }, cancellationToken);
            }

            Task<int> MyStatsServiceInterface.GetIntAsync(string key, CancellationToken cancellationToken)
            {
                return _state.RunAsync(() => _state.Stats.GetIntAsync(key, cancellationToken), cancellationToken);
            }

            Task MyStatsServiceInterface.ResetAsync(CancellationToken cancellationToken)
            {
                return _state.RunAsync(async () =>
                {
                    foreach (var definition in _state.Definitions.Stats ?? Array.Empty<MyProgressDefinitions.Stat>())
                        MyProgressDefinitions.EnsureWriter(definition.SetBy, _writer);

                    await _state.Stats.ResetAsync(cancellationToken);
                }, cancellationToken);
            }

            Task MyStatsServiceInterface.ResetAsync(string key, CancellationToken cancellationToken)
            {
                return _state.RunAsync(async () =>
                {
                    MyProgressDefinitions.EnsureWriter(_state.GetStat(key).SetBy, _writer);
                    await _state.Stats.ResetAsync(key, cancellationToken);
                }, cancellationToken);
            }

            Task MyStatsServiceInterface.SetAsync(string key, float value, CancellationToken cancellationToken)
            {
                return ChangeFloatAsync(key, value, false, cancellationToken);
            }

            Task MyStatsServiceInterface.SetAsync(string key, int value, CancellationToken cancellationToken)
            {
                return ChangeIntAsync(key, value, false, cancellationToken);
            }

            Task MyStatsServiceInterface.UpdateAverageRateAsync(string key, float count, double sessionLengthSeconds, CancellationToken cancellationToken)
            {
                return _state.RunAsync(async () =>
                {
                    var definition = _state.GetStat(key);
                    MyProgressDefinitions.EnsureWriter(definition.SetBy, _writer);
                    if (definition.Type != "AVGRATE")
                        throw new InvalidOperationException($"Stat '{key}' is not AVGRATE.");

                    if (definition.IncrementOnly || definition.MinValue.HasValue || definition.MaxValue.HasValue || definition.MaxChange.HasValue)
                        await _state.UpdateConstrainedAverageAsync(definition, count, sessionLengthSeconds, cancellationToken);
                    else
                        await _state.Stats.UpdateAverageRateAsync(key, count, sessionLengthSeconds / definition.SecondsPerUnit.Value, cancellationToken);

                    await _state.UnlockProgressAsync(key, cancellationToken);
                }, cancellationToken);
            }

            private Task ChangeFloatAsync(string key, float value, bool isAddition, CancellationToken cancellationToken)
            {
                return _state.RunAsync(async () =>
                {
                    var definition = _state.GetStat(key);
                    MyProgressDefinitions.EnsureWriter(definition.SetBy, _writer);
                    var previous = await _state.Stats.GetFloatAsync(key, cancellationToken);
                    var next = isAddition ? previous + value : value;
                    MyProgressDefinitions.ValidateChange(definition, previous, next);
                    await _state.Stats.SetAsync(key, next, cancellationToken);
                    await _state.UnlockProgressAsync(key, cancellationToken);
                }, cancellationToken);
            }

            private Task ChangeIntAsync(string key, int value, bool isAddition, CancellationToken cancellationToken)
            {
                return _state.RunAsync(async () =>
                {
                    var definition = _state.GetStat(key);
                    MyProgressDefinitions.EnsureWriter(definition.SetBy, _writer);
                    var previous = await _state.Stats.GetIntAsync(key, cancellationToken);
                    var next = isAddition ? checked(previous + value) : value;
                    MyProgressDefinitions.ValidateChange(definition, previous, next);
                    await _state.Stats.SetAsync(key, next, cancellationToken);
                    await _state.UnlockProgressAsync(key, cancellationToken);
                }, cancellationToken);
            }

            private void EnsureAggregated(string key, string type)
            {
                var definition = _state.GetStat(key);
                if ((definition.Aggregated == false) || ((definition.Type != type) && ((type != "FLOAT") || (definition.Type != "AVGRATE"))))
                    throw new InvalidOperationException($"Stat '{key}' is not an aggregated {type} stat.");
            }
        }

        private sealed class State
        {
            private readonly SemaphoreSlim _gate;
            private readonly Func<CancellationToken, Task<(bool IsFound, byte[] Data)>> _readDefinitionsAsync;
            private readonly Func<byte[], CancellationToken, Task> _writeDefinitionsAsync;

            private string _registeredJson;

            public MyAchievementServiceInterface Achievements { get; }
            public MyProgressDefinitions Definitions { get; private set; }
            public Func<string, CancellationToken, Task<double>> ReadGlobalFloatAsync { get; }
            public Func<string, CancellationToken, Task<long>> ReadGlobalIntAsync { get; }
            public MyStatsServiceInterface Stats { get; }

            public State(Func<CancellationToken, Task<(bool IsFound, byte[] Data)>> readStatsAsync, Func<byte[], CancellationToken, Task> writeStatsAsync, Func<CancellationToken, Task<(bool IsFound, byte[] Data)>> readAchievementsAsync, Func<byte[], CancellationToken, Task> writeAchievementsAsync, Func<CancellationToken, Task<(bool IsFound, byte[] Data)>> readDefinitionsAsync, Func<byte[], CancellationToken, Task> writeDefinitionsAsync, SemaphoreSlim gate, Func<string, CancellationToken, Task<long>> readGlobalIntAsync, Func<string, CancellationToken, Task<double>> readGlobalFloatAsync)
            {
                Achievements = MyPlatform.CreateAchievementService(readAchievementsAsync, writeAchievementsAsync);
                Stats = MyPlatform.CreateStatsService(readStatsAsync, writeStatsAsync);
                _readDefinitionsAsync = readDefinitionsAsync ?? throw new ArgumentNullException(nameof(readDefinitionsAsync));
                _writeDefinitionsAsync = writeDefinitionsAsync ?? throw new ArgumentNullException(nameof(writeDefinitionsAsync));
                _gate = gate ?? new SemaphoreSlim(1, 1);
                ReadGlobalIntAsync = readGlobalIntAsync ?? ((key, cancellationToken) => throw new NotSupportedException("This storage backend does not provide global stats."));
                ReadGlobalFloatAsync = readGlobalFloatAsync ?? ((key, cancellationToken) => throw new NotSupportedException("This storage backend does not provide global stats."));
            }

            public Task EnsureAsync(string json, bool isStats, CancellationToken cancellationToken, bool preserveStatMetadata = false)
            {
                var incoming = MyProgressDefinitions.Parse(json, false);
                if ((isStats && (incoming.Stats == null)) || ((isStats == false) && (incoming.Achievements == null)))
                    throw new InvalidDataException("The requested definitions array is missing.");

                return RunAsync(async () =>
                {
                    var next = MyProgressDefinitions.Copy(Definitions);
                    if (preserveStatMetadata)
                    {
                        for (var index = 0; index < incoming.Stats.Length; ++index)
                        {
                            var stat = incoming.Stats[index];
                            var previous = next.Stats?.FirstOrDefault(entry => entry.Key == stat.Key);
                            if (previous == null)
                                continue;

                            if ((previous.Type != stat.Type) || (previous.DefaultValue != stat.DefaultValue) || (previous.Window != stat.Window) || (previous.SecondsPerUnit != stat.SecondsPerUnit))
                                throw new InvalidOperationException($"Stat '{stat.Key}' is already registered with a different definition.");

                            incoming.Stats[index] = previous;
                        }
                    }

                    next.Stats = Merge(next.Stats, incoming.Stats, entry => entry.Key, entry => entry.Id, (entry, id) => entry.Id = id);
                    if (isStats == false)
                        next.Achievements = Merge(next.Achievements, incoming.Achievements, entry => entry.Key, entry => entry.Id, (entry, id) => entry.Id = id);

                    next = MyProgressDefinitions.Parse(next.ToJson());
                    foreach (var previous in Definitions.Stats ?? Array.Empty<MyProgressDefinitions.Stat>())
                    {
                        var stat = next.Stats.First(entry => entry.Key == previous.Key);
                        if ((previous.Type != stat.Type) || (previous.DefaultValue != stat.DefaultValue) || (previous.Window != stat.Window) || (previous.SecondsPerUnit != stat.SecondsPerUnit))
                            throw new InvalidOperationException($"Stat '{stat.Key}' is already registered with a different type, default value, or window unit.");
                    }

                    foreach (var previous in Definitions.Achievements ?? Array.Empty<MyProgressDefinitions.Achievement>())
                    {
                        var achievement = next.Achievements.First(entry => entry.Key == previous.Key);
                        if ((previous.StatKey != achievement.StatKey) || (previous.TargetValue != achievement.TargetValue))
                            throw new InvalidOperationException($"Achievement '{achievement.Key}' is already registered with a different progress definition.");
                    }

                    await RegisterAsync(next, cancellationToken);
                    await _writeDefinitionsAsync(Encoding.UTF8.GetBytes(next.ToJson()), cancellationToken);
                    Definitions = next;
                    _registeredJson = next.ToJson();
                    await UnlockProgressAsync(null, cancellationToken);
                }, cancellationToken);
            }

            public MyProgressDefinitions.Achievement GetAchievement(string key)
            {
                MyPlatform.EnsureAchievementKey(key);
                return Definitions.Achievements?.FirstOrDefault(entry => entry.Key == key) ?? throw new InvalidOperationException($"Achievement '{key}' is not registered. Call EnsureAsync first.");
            }

            public MyProgressDefinitions.Stat GetStat(string key)
            {
                MyPlatform.EnsureStatsKey(key);
                return Definitions.Stats?.FirstOrDefault(entry => entry.Key == key) ?? throw new InvalidOperationException($"Stat '{key}' is not registered. Call EnsureAsync first.");
            }

            private static T[] Merge<T>(T[] current, T[] incoming, Func<T, string> getKey, Func<T, int?> getId, Action<T, int> setId)
            {
                var entries = (current ?? Array.Empty<T>()).ToDictionary(getKey, StringComparer.Ordinal);
                var usedIds = new HashSet<int>(entries.Values.Where(entry => getId(entry).HasValue).Select(entry => getId(entry).Value));
                foreach (var entry in incoming ?? Array.Empty<T>())
                {
                    if (getId(entry).HasValue)
                        usedIds.Add(getId(entry).Value);
                }

                foreach (var entry in incoming ?? Array.Empty<T>())
                {
                    if (getId(entry).HasValue == false)
                    {
                        if (entries.TryGetValue(getKey(entry), out var previous) && getId(previous).HasValue)
                            setId(entry, getId(previous).Value);
                        else
                        {
                            var id = 0;
                            while (usedIds.Contains(id))
                                ++id;

                            setId(entry, id);
                        }
                    }

                    usedIds.Add(getId(entry).Value);
                    entries[getKey(entry)] = entry;
                }

                return entries.Values.OrderBy(getKey, StringComparer.Ordinal).ToArray();
            }

            private async Task RegisterAsync(MyProgressDefinitions definitions, CancellationToken cancellationToken)
            {
                foreach (var stat in definitions.Stats ?? Array.Empty<MyProgressDefinitions.Stat>())
                {
                    if (stat.Type == "INT")
                        await Stats.EnsureAsync(stat.Key, (int)stat.DefaultValue, cancellationToken);
                    else if (stat.Type == "FLOAT")
                        await Stats.EnsureAsync(stat.Key, (float)stat.DefaultValue, cancellationToken);
                    else
                        await Stats.EnsureAverageRateAsync(stat.Key, (float)stat.DefaultValue, stat.Window.Value, cancellationToken);
                }

                foreach (var achievement in definitions.Achievements ?? Array.Empty<MyProgressDefinitions.Achievement>())
                    await MyPlatform.EnsureAchievementAsync(Achievements, achievement.Key, achievement.StatKey, achievement.TargetValue.GetValueOrDefault(), cancellationToken);
            }

            public async Task RunAsync(Func<Task> action, CancellationToken cancellationToken)
            {
                await RunAsync(async () =>
                {
                    await action();
                    return true;
                }, cancellationToken);
            }

            public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
            {
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    var stored = await _readDefinitionsAsync(cancellationToken);
                    Definitions = stored.IsFound ? MyProgressDefinitions.Parse(Encoding.UTF8.GetString(stored.Data)) : new MyProgressDefinitions();
                    var json = Definitions.ToJson();
                    if (_registeredJson != json)
                    {
                        await RegisterAsync(Definitions, cancellationToken);
                        _registeredJson = json;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    return await action();
                }
                finally
                {
                    _gate.Release();
                }
            }

            public async Task UnlockProgressAsync(string statKey, CancellationToken cancellationToken)
            {
                foreach (var definition in Definitions.Achievements ?? Array.Empty<MyProgressDefinitions.Achievement>())
                {
                    if ((definition.StatKey == null) || ((statKey != null) && (definition.StatKey != statKey)))
                        continue;

                    var stat = GetStat(definition.StatKey);
                    var value = stat.Type == "INT" ? (double)await Stats.GetIntAsync(stat.Key, cancellationToken) : stat.Type == "FLOAT" ? await Stats.GetFloatAsync(stat.Key, cancellationToken) : await Stats.GetAverageRateAsync(stat.Key, cancellationToken);
                    if (value >= definition.TargetValue.Value)
                        await Achievements.UnlockAsync(definition.Key, cancellationToken);
                }
            }

            public Task UpdateConstrainedAverageAsync(MyProgressDefinitions.Stat definition, float count, double sessionLengthSeconds, CancellationToken cancellationToken)
            {
                return MyPlatform.UpdateConstrainedAverageAsync(Stats, definition, count, sessionLengthSeconds / definition.SecondsPerUnit.Value, cancellationToken);
            }
        }

        private readonly State _state;
        private readonly MyProgressDefinitions.WriterEnum _writer;

        public MyAchievementServiceInterface Achievements { get; }
        public MyStatsServiceInterface Stats { get; }

        public MyProgressServices(Func<CancellationToken, Task<(bool IsFound, byte[] Data)>> readStatsAsync, Func<byte[], CancellationToken, Task> writeStatsAsync, Func<CancellationToken, Task<(bool IsFound, byte[] Data)>> readAchievementsAsync, Func<byte[], CancellationToken, Task> writeAchievementsAsync, Func<CancellationToken, Task<(bool IsFound, byte[] Data)>> readDefinitionsAsync, Func<byte[], CancellationToken, Task> writeDefinitionsAsync, SemaphoreSlim gate = null, Func<string, CancellationToken, Task<long>> readGlobalIntAsync = null, Func<string, CancellationToken, Task<double>> readGlobalFloatAsync = null)
        {
            _state = new State(readStatsAsync, writeStatsAsync, readAchievementsAsync, writeAchievementsAsync, readDefinitionsAsync, writeDefinitionsAsync, gate, readGlobalIntAsync, readGlobalFloatAsync);
            Achievements = new ConfiguredMyAchievementService(_state, MyProgressDefinitions.WriterEnum.Client);
            Stats = new ConfiguredMyStatsService(_state, MyProgressDefinitions.WriterEnum.Client);
        }

        private MyProgressServices(State state, MyProgressDefinitions.WriterEnum writer)
        {
            if (Enum.IsDefined(typeof(MyProgressDefinitions.WriterEnum), writer) == false)
                throw new ArgumentOutOfRangeException(nameof(writer));

            _state = state;
            _writer = writer;
            Achievements = new ConfiguredMyAchievementService(state, writer);
            Stats = new ConfiguredMyStatsService(state, writer);
        }

        public MyProgressServices WithWriter(MyProgressDefinitions.WriterEnum writer)
        {
            return new MyProgressServices(_state, writer);
        }

        public Task ResetAllAsync(CancellationToken cancellationToken)
        {
            return _state.RunAsync(async () =>
            {
                foreach (var definition in _state.Definitions.Stats ?? Array.Empty<MyProgressDefinitions.Stat>())
                    MyProgressDefinitions.EnsureWriter(definition.SetBy, _writer);

                foreach (var definition in _state.Definitions.Achievements ?? Array.Empty<MyProgressDefinitions.Achievement>())
                    MyProgressDefinitions.EnsureWriter(definition.SetBy, _writer);

                await _state.Stats.ResetAsync(cancellationToken);
                await _state.Achievements.ResetAsync(cancellationToken);
            }, cancellationToken);
        }
    }
}
