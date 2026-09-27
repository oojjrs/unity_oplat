using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat
{
    internal static class MyPlatform
    {
        internal sealed class AchievementDefinition
        {
            public string Key { get; }
            public string StatKey { get; }
            public double? TargetValue { get; }

            public AchievementDefinition(string key, string statKey, double? targetValue)
            {
                Key = key;
                StatKey = statKey;
                TargetValue = targetValue;
            }
        }

        private enum StatsTypeEnum
        {
            AverageRate,
            Float,
            Int,
        }

        internal interface PlatformInterface : MyPlatformServiceInterface
        {
            Task RunAsync(MyPlatformInitializer.CallbackInterface callback, CancellationToken cancellationToken);
        }

        private sealed class StatsDefinition
        {
            public double AverageRateWindowSeconds { get; }
            public float DefaultFloatValue { get; }
            public int DefaultIntValue { get; }
            public string Key { get; }
            public StatsTypeEnum Type { get; }

            public StatsDefinition(string key, StatsTypeEnum type, int defaultIntValue, float defaultFloatValue, double averageRateWindowSeconds)
            {
                Key = key;
                Type = type;
                DefaultIntValue = defaultIntValue;
                DefaultFloatValue = defaultFloatValue;
                AverageRateWindowSeconds = averageRateWindowSeconds;
            }
        }

        [DataContract]
        private sealed class StatsDefinitionData
        {
            [DataMember(Name = "defaultValue")]
            public double DefaultValue { get; set; }

            [DataMember(Name = "key", IsRequired = true)]
            public string Key { get; set; }

            [DataMember(Name = "type", IsRequired = true)]
            public string Type { get; set; }

            [DataMember(Name = "windowSeconds")]
            public double WindowSeconds { get; set; }
        }

        [DataContract]
        private sealed class AchievementDefinitionData
        {
            [DataMember(Name = "key", IsRequired = true)]
            public string Key { get; set; }

            [DataMember(Name = "statKey")]
            public string StatKey { get; set; }

            [DataMember(Name = "targetValue")]
            public double? TargetValue { get; set; }
        }

        [DataContract]
        private sealed class ProgressDefinitionFile
        {
            [DataMember(Name = "achievements")]
            public AchievementDefinitionData[] Achievements { get; set; }

            [DataMember(Name = "stats")]
            public StatsDefinitionData[] Stats { get; set; }
        }

        private sealed class StoredAchievementService : MyAchievementServiceInterface
        {
            private sealed class StoredAchievement
            {
                public bool IsUnlocked { get; set; }
                public string StatKey { get; }
                public double? TargetValue { get; }

                public StoredAchievement(string statKey, double? targetValue, bool isUnlocked)
                {
                    StatKey = statKey;
                    TargetValue = targetValue;
                    IsUnlocked = isUnlocked;
                }
            }

            private const int EntryCountMax = 100000;
            private const int FormatMagic = 0x4F414331;
            private const int FormatVersion = 1;

            private readonly Dictionary<string, StoredAchievement> _entries = new(StringComparer.Ordinal);
            private readonly SemaphoreSlim _gate = new(1, 1);
            private readonly Func<CancellationToken, Task<(bool IsFound, byte[] Data)>> _readAsync;
            private readonly Func<byte[], CancellationToken, Task> _writeAsync;

            private bool _isLoaded;

            public StoredAchievementService(Func<CancellationToken, Task<(bool IsFound, byte[] Data)>> readAsync, Func<byte[], CancellationToken, Task> writeAsync)
            {
                _readAsync = readAsync ?? throw new ArgumentNullException(nameof(readAsync));
                _writeAsync = writeAsync ?? throw new ArgumentNullException(nameof(writeAsync));
            }

            Task MyAchievementServiceInterface.EnsureAsync(string definitionsJson, CancellationToken cancellationToken)
            {
                return EnsureAchievementsAsync(definitionsJson, EnsureAsync, cancellationToken);
            }

            async Task<bool> MyAchievementServiceInterface.IsUnlockedAsync(string key, CancellationToken cancellationToken)
            {
                EnsureAchievementKey(key);
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    await LoadAsync(cancellationToken);
                    return GetEntry(key).IsUnlocked;
                }
                finally
                {
                    _gate.Release();
                }
            }

            async Task MyAchievementServiceInterface.ResetAsync(CancellationToken cancellationToken)
            {
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    await LoadAsync(cancellationToken);
                    var unlockedKeys = new List<string>();
                    foreach (var pair in _entries)
                    {
                        if (pair.Value.IsUnlocked == false)
                            continue;

                        unlockedKeys.Add(pair.Key);
                        pair.Value.IsUnlocked = false;
                    }

                    if (unlockedKeys.Count == 0)
                        return;

                    try
                    {
                        await WriteAsync(cancellationToken);
                    }
                    catch
                    {
                        foreach (var key in unlockedKeys)
                            _entries[key].IsUnlocked = true;

                        throw;
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            async Task MyAchievementServiceInterface.ResetAsync(string key, CancellationToken cancellationToken)
            {
                EnsureAchievementKey(key);
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    await LoadAsync(cancellationToken);
                    var entry = GetEntry(key);
                    if (entry.IsUnlocked == false)
                        return;

                    entry.IsUnlocked = false;
                    try
                    {
                        await WriteAsync(cancellationToken);
                    }
                    catch
                    {
                        entry.IsUnlocked = true;
                        throw;
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            async Task MyAchievementServiceInterface.UnlockAsync(string key, CancellationToken cancellationToken)
            {
                EnsureAchievementKey(key);
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    await LoadAsync(cancellationToken);
                    var entry = GetEntry(key);
                    if (entry.IsUnlocked)
                        return;

                    entry.IsUnlocked = true;
                    try
                    {
                        await WriteAsync(cancellationToken);
                    }
                    catch
                    {
                        entry.IsUnlocked = false;
                        throw;
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            private void Deserialize(byte[] data)
            {
                if (data == null)
                    throw new InvalidDataException("Stored achievement data is missing.");

                using (var stream = new MemoryStream(data, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    if (reader.ReadInt32() != FormatMagic)
                        throw new InvalidDataException("Stored achievement data has an unsupported format.");

                    if (reader.ReadInt32() != FormatVersion)
                        throw new InvalidDataException("Stored achievement data has an unsupported format.");

                    var count = reader.ReadInt32();
                    if ((count < 0) || (count > EntryCountMax))
                        throw new InvalidDataException("Stored achievement data has an invalid entry count.");

                    for (var index = 0; index < count; ++index)
                    {
                        var key = reader.ReadString();
                        EnsureAchievementKey(key);
                        var hasProgress = reader.ReadBoolean();
                        var statKey = hasProgress ? reader.ReadString() : null;
                        double? targetValue = hasProgress ? reader.ReadDouble() : null;
                        if (hasProgress)
                        {
                            EnsureStatsKey(statKey);
                            if (double.IsNaN(targetValue.Value) || double.IsInfinity(targetValue.Value))
                                throw new InvalidDataException($"Stored achievement '{key}' has an invalid target value.");
                        }

                        var entry = new StoredAchievement(statKey, targetValue, reader.ReadBoolean());
                        if (_entries.TryAdd(key, entry) == false)
                            throw new InvalidDataException($"Stored achievement data contains duplicate key '{key}'.");
                    }

                    if (stream.Position != stream.Length)
                        throw new InvalidDataException("Stored achievement data contains trailing bytes.");
                }
            }

            internal async Task EnsureAsync(AchievementDefinition definition, CancellationToken cancellationToken)
            {
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    await LoadAsync(cancellationToken);
                    if (_entries.TryGetValue(definition.Key, out var entry))
                    {
                        if ((entry.StatKey != definition.StatKey) || (entry.TargetValue != definition.TargetValue))
                            throw new InvalidOperationException($"Achievement '{definition.Key}' is already registered with a different definition.");

                        return;
                    }

                    _entries.Add(definition.Key, new StoredAchievement(definition.StatKey, definition.TargetValue, false));
                    try
                    {
                        await WriteAsync(cancellationToken);
                    }
                    catch
                    {
                        _entries.Remove(definition.Key);
                        throw;
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            private StoredAchievement GetEntry(string key)
            {
                if (_entries.TryGetValue(key, out var entry))
                    return entry;

                throw new InvalidOperationException($"Achievement '{key}' is not registered. Call EnsureAsync first.");
            }

            private async Task LoadAsync(CancellationToken cancellationToken)
            {
                if (_isLoaded)
                    return;

                var result = await _readAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (result.IsFound)
                    Deserialize(result.Data);

                _isLoaded = true;
            }

            private byte[] Serialize()
            {
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(FormatMagic);
                    writer.Write(FormatVersion);
                    writer.Write(_entries.Count);
                    var keys = new List<string>(_entries.Keys);
                    keys.Sort(StringComparer.Ordinal);
                    foreach (var key in keys)
                    {
                        var entry = _entries[key];
                        writer.Write(key);
                        var hasProgress = entry.StatKey != null;
                        writer.Write(hasProgress);
                        if (hasProgress)
                        {
                            writer.Write(entry.StatKey);
                            writer.Write(entry.TargetValue.Value);
                        }

                        writer.Write(entry.IsUnlocked);
                    }

                    writer.Flush();
                    return stream.ToArray();
                }
            }

            private async Task WriteAsync(CancellationToken cancellationToken)
            {
                await _writeAsync(Serialize(), cancellationToken);
            }
        }

        private sealed class StoredStatsService : MyStatsServiceInterface
        {
            private sealed class StoredStat
            {
                public double AverageRateSampleLengthSeconds { get; set; }
                public double AverageRateWindowSeconds { get; }
                public float DefaultFloatValue { get; }
                public int DefaultIntValue { get; }
                public float FloatValue { get; set; }
                public int IntValue { get; set; }
                public StatsTypeEnum Type { get; }

                public StoredStat(StatsTypeEnum type, int defaultIntValue, int intValue, float defaultFloatValue, float floatValue, double averageRateWindowSeconds, double averageRateSampleLengthSeconds)
                {
                    Type = type;
                    DefaultIntValue = defaultIntValue;
                    IntValue = intValue;
                    DefaultFloatValue = defaultFloatValue;
                    FloatValue = floatValue;
                    AverageRateWindowSeconds = averageRateWindowSeconds;
                    AverageRateSampleLengthSeconds = averageRateSampleLengthSeconds;
                }
            }

            private const int EntryCountMax = 100000;
            private const int FormatMagic = 0x4F535431;
            private const int FormatVersion = 2;

            private readonly Dictionary<string, StoredStat> _entries = new(StringComparer.Ordinal);
            private readonly SemaphoreSlim _gate = new(1, 1);
            private readonly Func<CancellationToken, Task<(bool IsFound, byte[] Data)>> _readAsync;
            private readonly Func<byte[], CancellationToken, Task> _writeAsync;

            private bool _isLoaded;

            public StoredStatsService(Func<CancellationToken, Task<(bool IsFound, byte[] Data)>> readAsync, Func<byte[], CancellationToken, Task> writeAsync)
            {
                _readAsync = readAsync ?? throw new ArgumentNullException(nameof(readAsync));
                _writeAsync = writeAsync ?? throw new ArgumentNullException(nameof(writeAsync));
            }

            async Task MyStatsServiceInterface.AddAsync(string key, float value, CancellationToken cancellationToken)
            {
                EnsureStatsKey(key);
                EnsureFinite(value, nameof(value));
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    await LoadAsync(cancellationToken);
                    var entry = GetEntry(key, StatsTypeEnum.Float);
                    var previousValue = entry.FloatValue;
                    var addedValue = previousValue + value;
                    EnsureFinite(addedValue, nameof(value));
                    entry.FloatValue = addedValue;
                    try
                    {
                        await WriteAsync(cancellationToken);
                    }
                    catch
                    {
                        entry.FloatValue = previousValue;
                        throw;
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            async Task MyStatsServiceInterface.AddAsync(string key, int value, CancellationToken cancellationToken)
            {
                EnsureStatsKey(key);
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    await LoadAsync(cancellationToken);
                    var entry = GetEntry(key, StatsTypeEnum.Int);
                    var previousValue = entry.IntValue;
                    entry.IntValue = checked(previousValue + value);
                    try
                    {
                        await WriteAsync(cancellationToken);
                    }
                    catch
                    {
                        entry.IntValue = previousValue;
                        throw;
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            Task MyStatsServiceInterface.EnsureAsync(string definitionsJson, CancellationToken cancellationToken)
            {
                return EnsureStatsAsync(this, definitionsJson, cancellationToken);
            }

            async Task MyStatsServiceInterface.EnsureAsync(string key, float defaultValue, CancellationToken cancellationToken)
            {
                EnsureStatsKey(key);
                EnsureFinite(defaultValue, nameof(defaultValue));
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    await LoadAsync(cancellationToken);
                    if (_entries.TryGetValue(key, out var entry))
                    {
                        EnsureDefinition(key, entry, StatsTypeEnum.Float, 0, defaultValue, 0d);
                        return;
                    }

                    _entries.Add(key, new StoredStat(StatsTypeEnum.Float, 0, 0, defaultValue, defaultValue, 0d, 0d));
                    try
                    {
                        await WriteAsync(cancellationToken);
                    }
                    catch
                    {
                        _entries.Remove(key);
                        throw;
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            async Task MyStatsServiceInterface.EnsureAsync(string key, int defaultValue, CancellationToken cancellationToken)
            {
                EnsureStatsKey(key);
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    await LoadAsync(cancellationToken);
                    if (_entries.TryGetValue(key, out var entry))
                    {
                        EnsureDefinition(key, entry, StatsTypeEnum.Int, defaultValue, 0f, 0d);
                        return;
                    }

                    _entries.Add(key, new StoredStat(StatsTypeEnum.Int, defaultValue, defaultValue, 0f, 0f, 0d, 0d));
                    try
                    {
                        await WriteAsync(cancellationToken);
                    }
                    catch
                    {
                        _entries.Remove(key);
                        throw;
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            async Task MyStatsServiceInterface.EnsureAverageRateAsync(string key, float defaultValue, double windowSeconds, CancellationToken cancellationToken)
            {
                EnsureStatsKey(key);
                EnsureFinite(defaultValue, nameof(defaultValue));
                EnsurePositiveFinite(windowSeconds, nameof(windowSeconds));
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    await LoadAsync(cancellationToken);
                    if (_entries.TryGetValue(key, out var entry))
                    {
                        EnsureDefinition(key, entry, StatsTypeEnum.AverageRate, 0, defaultValue, windowSeconds);
                        return;
                    }

                    _entries.Add(key, new StoredStat(StatsTypeEnum.AverageRate, 0, 0, defaultValue, defaultValue, windowSeconds, 0d));
                    try
                    {
                        await WriteAsync(cancellationToken);
                    }
                    catch
                    {
                        _entries.Remove(key);
                        throw;
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            async Task<float> MyStatsServiceInterface.GetAverageRateAsync(string key, CancellationToken cancellationToken)
            {
                EnsureStatsKey(key);
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    await LoadAsync(cancellationToken);
                    return GetEntry(key, StatsTypeEnum.AverageRate).FloatValue;
                }
                finally
                {
                    _gate.Release();
                }
            }

            async Task<float> MyStatsServiceInterface.GetFloatAsync(string key, CancellationToken cancellationToken)
            {
                EnsureStatsKey(key);
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    await LoadAsync(cancellationToken);
                    return GetEntry(key, StatsTypeEnum.Float).FloatValue;
                }
                finally
                {
                    _gate.Release();
                }
            }

            async Task<int> MyStatsServiceInterface.GetIntAsync(string key, CancellationToken cancellationToken)
            {
                EnsureStatsKey(key);
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    await LoadAsync(cancellationToken);
                    return GetEntry(key, StatsTypeEnum.Int).IntValue;
                }
                finally
                {
                    _gate.Release();
                }
            }

            async Task MyStatsServiceInterface.ResetAsync(CancellationToken cancellationToken)
            {
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    await LoadAsync(cancellationToken);
                    if (_entries.Count == 0)
                        return;

                    var entries = CloneEntries();
                    foreach (var entry in _entries.Values)
                        Reset(entry);

                    try
                    {
                        await WriteAsync(cancellationToken);
                    }
                    catch
                    {
                        Restore(entries);
                        throw;
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            async Task MyStatsServiceInterface.ResetAsync(string key, CancellationToken cancellationToken)
            {
                EnsureStatsKey(key);
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    await LoadAsync(cancellationToken);
                    var entry = GetEntry(key);
                    var previousEntry = Clone(entry);
                    Reset(entry);
                    try
                    {
                        await WriteAsync(cancellationToken);
                    }
                    catch
                    {
                        _entries[key] = previousEntry;
                        throw;
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            async Task MyStatsServiceInterface.UpdateAverageRateAsync(string key, float count, double sessionLengthSeconds, CancellationToken cancellationToken)
            {
                EnsureStatsKey(key);
                EnsureFinite(count, nameof(count));
                EnsurePositiveFinite(sessionLengthSeconds, nameof(sessionLengthSeconds));
                await _gate.WaitAsync(cancellationToken);
                try
                {
                    await LoadAsync(cancellationToken);
                    var entry = GetEntry(key, StatsTypeEnum.AverageRate);
                    var previousValue = entry.FloatValue;
                    var previousSampleLengthSeconds = entry.AverageRateSampleLengthSeconds;
                    var currentSampleLengthSeconds = Math.Min(sessionLengthSeconds, entry.AverageRateWindowSeconds);
                    var previousRetainedLengthSeconds = Math.Min(previousSampleLengthSeconds, entry.AverageRateWindowSeconds - currentSampleLengthSeconds);
                    var retainedCount = count * currentSampleLengthSeconds / sessionLengthSeconds;
                    var combinedLengthSeconds = previousRetainedLengthSeconds + currentSampleLengthSeconds;
                    var averageRate = ((previousValue * previousRetainedLengthSeconds) + retainedCount) / combinedLengthSeconds;
                    EnsureFinite(averageRate, nameof(count));
                    var averageRateValue = (float)averageRate;
                    EnsureFinite(averageRateValue, nameof(count));
                    entry.FloatValue = averageRateValue;
                    entry.AverageRateSampleLengthSeconds = combinedLengthSeconds;
                    try
                    {
                        await WriteAsync(cancellationToken);
                    }
                    catch
                    {
                        entry.FloatValue = previousValue;
                        entry.AverageRateSampleLengthSeconds = previousSampleLengthSeconds;
                        throw;
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            private static StoredStat Clone(StoredStat entry)
            {
                return new StoredStat(entry.Type, entry.DefaultIntValue, entry.IntValue, entry.DefaultFloatValue, entry.FloatValue, entry.AverageRateWindowSeconds, entry.AverageRateSampleLengthSeconds);
            }

            private Dictionary<string, StoredStat> CloneEntries()
            {
                var entries = new Dictionary<string, StoredStat>(_entries.Count, StringComparer.Ordinal);
                foreach (var pair in _entries)
                    entries.Add(pair.Key, Clone(pair.Value));

                return entries;
            }

            private async Task LoadAsync(CancellationToken cancellationToken)
            {
                if (_isLoaded)
                    return;

                var result = await _readAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (result.IsFound)
                    Deserialize(result.Data);

                _isLoaded = true;
            }

            private void Deserialize(byte[] data)
            {
                if (data == null)
                    throw new InvalidDataException("Stored stats data is missing.");

                using (var stream = new MemoryStream(data, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    if (reader.ReadInt32() != FormatMagic)
                        throw new InvalidDataException("Stored stats data has an unsupported format.");

                    var version = reader.ReadInt32();
                    var count = reader.ReadInt32();
                    if ((count < 0) || (count > EntryCountMax))
                        throw new InvalidDataException("Stored stats data has an invalid entry count.");

                    for (var index = 0; index < count; ++index)
                    {
                        var key = reader.ReadString();
                        EnsureStatsKey(key);
                        StoredStat entry;
                        if (version == 1)
                        {
                            var value = reader.ReadInt32();
                            entry = new StoredStat(StatsTypeEnum.Int, 0, value, 0f, 0f, 0d, 0d);
                        }
                        else if (version == FormatVersion)
                        {
                            entry = ReadEntry(reader);
                        }
                        else
                        {
                            throw new InvalidDataException("Stored stats data has an unsupported format.");
                        }

                        if (_entries.TryAdd(key, entry) == false)
                            throw new InvalidDataException($"Stored stats data contains duplicate key '{key}'.");
                    }

                    if (stream.Position != stream.Length)
                        throw new InvalidDataException("Stored stats data contains trailing bytes.");
                }
            }

            private static void EnsureDefinition(string key, StoredStat entry, StatsTypeEnum type, int defaultIntValue, float defaultFloatValue, double averageRateWindowSeconds)
            {
                if (entry.Type != type)
                    throw new InvalidOperationException($"Stat '{key}' is already registered as {entry.Type}.");

                if ((entry.DefaultIntValue != defaultIntValue) || (entry.DefaultFloatValue != defaultFloatValue) || (entry.AverageRateWindowSeconds != averageRateWindowSeconds))
                    throw new InvalidOperationException($"Stat '{key}' is already registered with a different definition.");
            }

            private static void EnsureFinite(double value, string parameterName)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(parameterName);
            }

            private static void EnsureFinite(float value, string parameterName)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(parameterName);
            }

            private static void EnsurePositiveFinite(double value, string parameterName)
            {
                EnsureFinite(value, parameterName);
                if (value <= 0d)
                    throw new ArgumentOutOfRangeException(parameterName);
            }

            private StoredStat GetEntry(string key)
            {
                if (_entries.TryGetValue(key, out var entry))
                    return entry;

                throw new InvalidOperationException($"Stat '{key}' is not registered. Call EnsureAsync first.");
            }

            private StoredStat GetEntry(string key, StatsTypeEnum type)
            {
                var entry = GetEntry(key);
                if (entry.Type != type)
                    throw new InvalidOperationException($"Stat '{key}' is registered as {entry.Type}, not {type}.");

                return entry;
            }

            private static StoredStat ReadEntry(BinaryReader reader)
            {
                var type = (StatsTypeEnum)reader.ReadByte();
                switch (type)
                {
                    case StatsTypeEnum.Int:
                        return new StoredStat(type, reader.ReadInt32(), reader.ReadInt32(), 0f, 0f, 0d, 0d);
                    case StatsTypeEnum.Float:
                    {
                        var defaultValue = reader.ReadSingle();
                        var value = reader.ReadSingle();
                        EnsureFinite(defaultValue, nameof(defaultValue));
                        EnsureFinite(value, nameof(value));
                        return new StoredStat(type, 0, 0, defaultValue, value, 0d, 0d);
                    }
                    case StatsTypeEnum.AverageRate:
                    {
                        var defaultValue = reader.ReadSingle();
                        var value = reader.ReadSingle();
                        var windowSeconds = reader.ReadDouble();
                        var sampleLengthSeconds = reader.ReadDouble();
                        EnsureFinite(defaultValue, nameof(defaultValue));
                        EnsureFinite(value, nameof(value));
                        EnsurePositiveFinite(windowSeconds, nameof(windowSeconds));
                        EnsureFinite(sampleLengthSeconds, nameof(sampleLengthSeconds));
                        if ((sampleLengthSeconds < 0d) || (sampleLengthSeconds > windowSeconds))
                            throw new InvalidDataException("Stored average-rate stat has an invalid sample length.");

                        return new StoredStat(type, 0, 0, defaultValue, value, windowSeconds, sampleLengthSeconds);
                    }
                    default:
                        throw new InvalidDataException("Stored stats data contains an unsupported stat type.");
                }
            }

            private static void Reset(StoredStat entry)
            {
                entry.IntValue = entry.DefaultIntValue;
                entry.FloatValue = entry.DefaultFloatValue;
                entry.AverageRateSampleLengthSeconds = 0d;
            }

            private void Restore(Dictionary<string, StoredStat> entries)
            {
                _entries.Clear();
                foreach (var pair in entries)
                    _entries.Add(pair.Key, pair.Value);
            }

            private byte[] Serialize()
            {
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(FormatMagic);
                    writer.Write(FormatVersion);
                    writer.Write(_entries.Count);
                    var keys = new List<string>(_entries.Keys);
                    keys.Sort(StringComparer.Ordinal);
                    foreach (var key in keys)
                    {
                        writer.Write(key);
                        var entry = _entries[key];
                        writer.Write((byte)entry.Type);
                        switch (entry.Type)
                        {
                            case StatsTypeEnum.Int:
                                writer.Write(entry.DefaultIntValue);
                                writer.Write(entry.IntValue);
                                break;
                            case StatsTypeEnum.Float:
                                writer.Write(entry.DefaultFloatValue);
                                writer.Write(entry.FloatValue);
                                break;
                            case StatsTypeEnum.AverageRate:
                                writer.Write(entry.DefaultFloatValue);
                                writer.Write(entry.FloatValue);
                                writer.Write(entry.AverageRateWindowSeconds);
                                writer.Write(entry.AverageRateSampleLengthSeconds);
                                break;
                            default:
                                throw new InvalidOperationException("Stored stats data contains an unsupported stat type.");
                        }
                    }

                    writer.Flush();
                    return stream.ToArray();
                }
            }

            private async Task WriteAsync(CancellationToken cancellationToken)
            {
                await _writeAsync(Serialize(), cancellationToken);
            }
        }

        private sealed class TimeService : MyTimeServiceInterface
        {
            private readonly bool _isSynchronized;
            private readonly TimeSpan _localClockOffset;
            private readonly MyTime _originTime;
            private readonly long _originTimestamp;

            bool MyTimeServiceInterface.IsSynchronized => _isSynchronized;
            TimeSpan MyTimeServiceInterface.LocalClockOffset => _localClockOffset;
            MyTime MyTimeServiceInterface.UtcNow
            {
                get
                {
                    var elapsedTimestamp = Stopwatch.GetTimestamp() - _originTimestamp;
                    var elapsedSeconds = elapsedTimestamp / Stopwatch.Frequency;
                    var remainingTimestamp = elapsedTimestamp % Stopwatch.Frequency;
                    var elapsedTicks = checked((elapsedSeconds * TimeSpan.TicksPerSecond) + (remainingTimestamp * TimeSpan.TicksPerSecond / Stopwatch.Frequency));
                    return MyTime.FromUtcTicks(checked(_originTime.UtcTicks + elapsedTicks));
                }
            }

            public TimeService(MyTime originTime, bool isSynchronized)
            {
                _originTime = originTime;
                _originTimestamp = Stopwatch.GetTimestamp();
                _isSynchronized = isSynchronized;
                _localClockOffset = isSynchronized ? originTime - MyTime.FromUtcDateTime(DateTime.UtcNow) : TimeSpan.Zero;
            }
        }

        private static readonly Dictionary<MyPlatformTypeEnum, Func<PlatformInterface>> __platformFactories = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearRegistrations()
        {
            __platformFactories.Clear();
        }

        internal static PlatformInterface CreateComponent<T>() where T : MonoBehaviour, PlatformInterface
        {
            var gameObject = new GameObject(typeof(T).Name);
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            return gameObject.AddComponent<T>();
        }

        internal static PlatformInterface CreatePlatform(MyPlatformTypeEnum type)
        {
            if (__platformFactories.TryGetValue(type, out var platformFactory))
                return platformFactory();

            throw new NotImplementedException();
        }

        internal static MyAchievementServiceInterface CreateAchievementService(Func<CancellationToken, Task<(bool IsFound, byte[] Data)>> readAsync, Func<byte[], CancellationToken, Task> writeAsync)
        {
            return new StoredAchievementService(readAsync, writeAsync);
        }

        public static MyStatsServiceInterface CreateStatsService(Func<CancellationToken, Task<(bool IsFound, byte[] Data)>> readAsync, Func<byte[], CancellationToken, Task> writeAsync)
        {
            return new StoredStatsService(readAsync, writeAsync);
        }

        internal static async Task EnsureAchievementsAsync(string definitionsJson, Func<AchievementDefinition, CancellationToken, Task> ensureAsync, CancellationToken cancellationToken)
        {
            if (ensureAsync == null)
                throw new ArgumentNullException(nameof(ensureAsync));

            var definitions = ParseAchievementDefinitions(definitionsJson);
            foreach (var definition in definitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ensureAsync(definition, cancellationToken);
            }
        }

        internal static Task EnsureAchievementAsync(MyAchievementServiceInterface service, string key, string statKey, double? targetValue, CancellationToken cancellationToken)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            if (service is not StoredAchievementService storedService)
                throw new ArgumentException("The achievement service does not support direct registration.", nameof(service));

            EnsureAchievementKey(key);
            if ((statKey != null) && string.IsNullOrWhiteSpace(statKey))
                throw new ArgumentException("An achievement stat key cannot be empty.", nameof(statKey));

            if ((statKey != null) != targetValue.HasValue)
                throw new ArgumentException("An achievement stat key and target value must be provided together.", nameof(statKey));

            if (statKey != null)
            {
                EnsureStatsKey(statKey);
                if (double.IsNaN(targetValue.Value) || double.IsInfinity(targetValue.Value))
                    throw new ArgumentOutOfRangeException(nameof(targetValue));
            }

            return storedService.EnsureAsync(new AchievementDefinition(key, statKey, targetValue), cancellationToken);
        }

        public static async Task EnsureStatsAsync(MyStatsServiceInterface service, string definitionsJson, CancellationToken cancellationToken)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            var definitions = ParseStatsDefinitions(definitionsJson);
            foreach (var definition in definitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (definition.Type)
                {
                    case StatsTypeEnum.Int:
                        await service.EnsureAsync(definition.Key, definition.DefaultIntValue, cancellationToken);
                        break;
                    case StatsTypeEnum.Float:
                        await service.EnsureAsync(definition.Key, definition.DefaultFloatValue, cancellationToken);
                        break;
                    case StatsTypeEnum.AverageRate:
                        await service.EnsureAverageRateAsync(definition.Key, definition.DefaultFloatValue, definition.AverageRateWindowSeconds, cancellationToken);
                        break;
                    default:
                        throw new InvalidDataException("Stats definitions contain an unsupported stat type.");
                }
            }
        }

        public static MyTimeServiceInterface CreateTimeService(MyTime originTime, bool isSynchronized)
        {
            return new TimeService(originTime, isSynchronized);
        }

        public static MyTimeServiceInterface CreateTimeServiceFromLocalClock()
        {
            return CreateTimeService(MyTime.FromUtcDateTime(DateTime.UtcNow), false);
        }

        public static void EnsureStatsKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A stat key is required.", nameof(key));

            if (Encoding.UTF8.GetByteCount(key) >= 128)
                throw new ArgumentException("A stat key must be shorter than 128 UTF-8 bytes.", nameof(key));
        }

        public static void EnsureAchievementKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("An achievement key is required.", nameof(key));

            if (Encoding.UTF8.GetByteCount(key) >= 128)
                throw new ArgumentException("An achievement key must be shorter than 128 UTF-8 bytes.", nameof(key));
        }

        private static IReadOnlyList<AchievementDefinition> ParseAchievementDefinitions(string definitionsJson)
        {
            var file = ParseProgressDefinitionFile(definitionsJson);
            if (file?.Achievements == null)
                throw new InvalidDataException("Progress definitions must contain an achievements array.");

            var stats = ParseStatsDefinitions(file, false);
            var statsByKey = new Dictionary<string, StatsDefinition>(stats.Count, StringComparer.Ordinal);
            foreach (var stat in stats)
                statsByKey.Add(stat.Key, stat);

            var definitions = new List<AchievementDefinition>(file.Achievements.Length);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var data in file.Achievements)
            {
                if (data == null)
                    throw new InvalidDataException("Achievement definitions contain an empty item.");

                EnsureAchievementKey(data.Key);
                if (keys.Add(data.Key) == false)
                    throw new InvalidDataException($"Achievement definitions contain duplicate key '{data.Key}'.");

                if ((data.StatKey != null) && string.IsNullOrWhiteSpace(data.StatKey))
                    throw new InvalidDataException($"Achievement '{data.Key}' has an invalid statKey.");

                var hasStatKey = data.StatKey != null;
                var hasTargetValue = data.TargetValue.HasValue;
                if (hasStatKey != hasTargetValue)
                    throw new InvalidDataException($"Achievement '{data.Key}' must define statKey and targetValue together.");

                if (hasStatKey == false)
                {
                    definitions.Add(new AchievementDefinition(data.Key, null, null));
                    continue;
                }

                EnsureStatsKey(data.StatKey);
                if (statsByKey.TryGetValue(data.StatKey, out var stat) == false)
                    throw new InvalidDataException($"Achievement '{data.Key}' references undefined stat '{data.StatKey}'.");

                var targetValue = data.TargetValue.Value;
                if (double.IsNaN(targetValue) || double.IsInfinity(targetValue))
                    throw new InvalidDataException($"Achievement '{data.Key}' has an invalid targetValue.");

                if (stat.Type == StatsTypeEnum.Int)
                {
                    if ((targetValue < int.MinValue) || (targetValue > int.MaxValue) || (targetValue != Math.Truncate(targetValue)))
                        throw new InvalidDataException($"Achievement '{data.Key}' has a targetValue incompatible with INT stat '{data.StatKey}'.");
                }
                else
                {
                    var floatTargetValue = (float)targetValue;
                    if (float.IsInfinity(floatTargetValue))
                        throw new InvalidDataException($"Achievement '{data.Key}' has a targetValue incompatible with stat '{data.StatKey}'.");
                }

                definitions.Add(new AchievementDefinition(data.Key, data.StatKey, targetValue));
            }

            return definitions;
        }

        private static ProgressDefinitionFile ParseProgressDefinitionFile(string definitionsJson)
        {
            if (string.IsNullOrWhiteSpace(definitionsJson))
                throw new ArgumentException("Progress definitions JSON is required.", nameof(definitionsJson));

            var bytes = Encoding.UTF8.GetBytes(definitionsJson);
            using (var stream = new MemoryStream(bytes, false))
                return (ProgressDefinitionFile)new DataContractJsonSerializer(typeof(ProgressDefinitionFile)).ReadObject(stream);
        }

        private static IReadOnlyList<StatsDefinition> ParseStatsDefinitions(string definitionsJson)
        {
            return ParseStatsDefinitions(ParseProgressDefinitionFile(definitionsJson), true);
        }

        private static IReadOnlyList<StatsDefinition> ParseStatsDefinitions(ProgressDefinitionFile file, bool isRequired)
        {
            if (file?.Stats == null)
            {
                if (isRequired)
                    throw new InvalidDataException("Progress definitions must contain a stats array.");

                return Array.Empty<StatsDefinition>();
            }

            var definitions = new List<StatsDefinition>(file.Stats.Length);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var data in file.Stats)
            {
                if (data == null)
                    throw new InvalidDataException("Stats definitions contain an empty item.");

                EnsureStatsKey(data.Key);
                if (keys.Add(data.Key) == false)
                    throw new InvalidDataException($"Stats definitions contain duplicate key '{data.Key}'.");

                if (double.IsNaN(data.DefaultValue) || double.IsInfinity(data.DefaultValue))
                    throw new InvalidDataException($"Stat '{data.Key}' has an invalid defaultValue.");

                switch (data.Type)
                {
                    case "INT":
                        if ((data.DefaultValue < int.MinValue) || (data.DefaultValue > int.MaxValue) || (data.DefaultValue != Math.Truncate(data.DefaultValue)) || (data.WindowSeconds != 0d))
                            throw new InvalidDataException($"INT stat '{data.Key}' has an invalid definition.");

                        definitions.Add(new StatsDefinition(data.Key, StatsTypeEnum.Int, (int)data.DefaultValue, 0f, 0d));
                        break;
                    case "FLOAT":
                    {
                        var defaultValue = (float)data.DefaultValue;
                        if (float.IsInfinity(defaultValue) || (data.WindowSeconds != 0d))
                            throw new InvalidDataException($"FLOAT stat '{data.Key}' has an invalid definition.");

                        definitions.Add(new StatsDefinition(data.Key, StatsTypeEnum.Float, 0, defaultValue, 0d));
                        break;
                    }
                    case "AVGRATE":
                    {
                        var defaultValue = (float)data.DefaultValue;
                        if (float.IsInfinity(defaultValue) || double.IsNaN(data.WindowSeconds) || double.IsInfinity(data.WindowSeconds) || (data.WindowSeconds <= 0d))
                            throw new InvalidDataException($"AVGRATE stat '{data.Key}' has an invalid definition.");

                        definitions.Add(new StatsDefinition(data.Key, StatsTypeEnum.AverageRate, 0, defaultValue, data.WindowSeconds));
                        break;
                    }
                    default:
                        throw new InvalidDataException($"Stat '{data.Key}' has unsupported type '{data.Type}'.");
                }
            }

            return definitions;
        }

        internal static void DestroyPlatform(PlatformInterface platform)
        {
            var component = platform as MonoBehaviour;
            if (component != null)
                UnityEngine.Object.Destroy(component.gameObject);
        }

        internal static void Register(MyPlatformTypeEnum type, Func<PlatformInterface> platformFactory)
        {
            __platformFactories.Add(type, platformFactory);
        }
    }
}
