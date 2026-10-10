using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace oojjrs.oplat
{
    [DataContract]
    public sealed class MyProgressDefinitions
    {
        public enum WriterEnum
        {
            Client,
            GameServer,
            OfficialGameServer,
        }

        [DataContract]
        public sealed class LocalizedText
        {
            [DataMember(Name = "description", EmitDefaultValue = false)]
            public string Description { get; set; }
            [DataMember(Name = "displayName", EmitDefaultValue = false)]
            public string DisplayName { get; set; }
            [DataMember(Name = "language", IsRequired = true)]
            public string Language { get; set; }
        }

        [DataContract]
        public sealed class Achievement
        {
            [DataMember(Name = "achievedIcon", EmitDefaultValue = false)]
            public string AchievedIcon { get; set; }
            [DataMember(Name = "desc", EmitDefaultValue = false)]
            public string Desc { get; set; }
            [DataMember(Name = "description", EmitDefaultValue = false)]
            public string Description { get; set; }
            [DataMember(Name = "displayName", EmitDefaultValue = false)]
            public string DisplayName { get; set; }
            [DataMember(Name = "hidden")]
            public bool Hidden { get; set; }
            [DataMember(Name = "id", EmitDefaultValue = false)]
            public int? Id { get; set; }
            [DataMember(Name = "key", IsRequired = true)]
            public string Key { get; set; }
            [DataMember(Name = "localizations", EmitDefaultValue = false)]
            public LocalizedText[] Localizations { get; set; }
            [DataMember(Name = "setBy", EmitDefaultValue = false)]
            public string SetBy { get; set; }
            [DataMember(Name = "statKey", EmitDefaultValue = false)]
            public string StatKey { get; set; }
            [DataMember(Name = "targetValue", EmitDefaultValue = false)]
            public double? TargetValue { get; set; }
            [DataMember(Name = "title", EmitDefaultValue = false)]
            public string Title { get; set; }
            [DataMember(Name = "unachievedIcon", EmitDefaultValue = false)]
            public string UnachievedIcon { get; set; }

            public Achievement Localize(string language)
            {
                var result = Copy(this);
                var text = FindLocalization(Localizations, language);
                if (text != null)
                {
                    result.DisplayName = text.DisplayName ?? DisplayName;
                    result.Description = text.Description ?? Description;
                }

                return result;
            }
        }

        [DataContract]
        public sealed class Stat
        {
            [DataMember(Name = "aggregated")]
            public bool Aggregated { get; set; }
            [DataMember(Name = "defaultValue")]
            public double DefaultValue { get; set; }
            [DataMember(Name = "displayName", EmitDefaultValue = false)]
            public string DisplayName { get; set; }
            [DataMember(Name = "id", EmitDefaultValue = false)]
            public int? Id { get; set; }
            [DataMember(Name = "incrementOnly")]
            public bool IncrementOnly { get; set; }
            [DataMember(Name = "key", IsRequired = true)]
            public string Key { get; set; }
            [DataMember(Name = "localizations", EmitDefaultValue = false)]
            public LocalizedText[] Localizations { get; set; }
            [DataMember(Name = "maxChange", EmitDefaultValue = false)]
            public double? MaxChange { get; set; }
            [DataMember(Name = "maxValue", EmitDefaultValue = false)]
            public double? MaxValue { get; set; }
            [DataMember(Name = "minValue", EmitDefaultValue = false)]
            public double? MinValue { get; set; }
            [DataMember(Name = "secondsPerUnit", EmitDefaultValue = false)]
            public double? SecondsPerUnit { get; set; }
            [DataMember(Name = "setBy", EmitDefaultValue = false)]
            public string SetBy { get; set; }
            [DataMember(Name = "type", IsRequired = true)]
            public string Type { get; set; }
            [DataMember(Name = "window", EmitDefaultValue = false)]
            public double? Window { get; set; }
            [DataMember(Name = "windowSeconds", EmitDefaultValue = false)]
            public double? WindowSeconds { get; set; }

            public Stat Localize(string language)
            {
                var result = Copy(this);
                var text = FindLocalization(Localizations, language);
                if (text != null)
                    result.DisplayName = text.DisplayName ?? DisplayName;

                return result;
            }
        }

        [DataMember(Name = "achievements", EmitDefaultValue = false)]
        public Achievement[] Achievements { get; set; }
        [DataMember(Name = "stats", EmitDefaultValue = false)]
        public Stat[] Stats { get; set; }

        public static T Copy<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                stream.Position = 0;
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
            }
        }

        public static void EnsureWriter(string setBy, WriterEnum writer)
        {
            if (Enum.IsDefined(typeof(WriterEnum), writer) == false)
                throw new ArgumentOutOfRangeException(nameof(writer));

            var required = ParseWriter(setBy);
            if ((writer != required) && ((required != WriterEnum.GameServer) || (writer != WriterEnum.OfficialGameServer)))
                throw new UnauthorizedAccessException($"Progress entry can only be changed by {required}.");
        }

        private static LocalizedText FindLocalization(LocalizedText[] localizations, string language)
        {
            return localizations?.FirstOrDefault(text => string.Equals(text.Language, language, StringComparison.OrdinalIgnoreCase)) ?? localizations?.FirstOrDefault(text => string.Equals(text.Language, "english", StringComparison.OrdinalIgnoreCase));
        }

        public static MyProgressDefinitions Parse(string json, bool requireReferencedStats = true)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Progress definitions JSON is required.", nameof(json));

            MyProgressDefinitions result;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json), false))
                result = (MyProgressDefinitions)new DataContractJsonSerializer(typeof(MyProgressDefinitions)).ReadObject(stream);

            if (result == null)
                throw new InvalidDataException("Progress definitions are missing.");

            var stats = new Dictionary<string, Stat>(StringComparer.Ordinal);
            var statIds = new HashSet<int>();
            foreach (var stat in result.Stats ?? Array.Empty<Stat>())
            {
                if (stat == null)
                    throw new InvalidDataException("Stat definitions contain an empty item.");

                MyPlatform.EnsureStatsKey(stat.Key);
                if (stats.TryAdd(stat.Key, stat) == false)
                    throw new InvalidDataException($"Duplicate stat '{stat.Key}'.");

                ValidateId(stat.Id, statIds);
                stat.SetBy = ParseWriter(stat.SetBy).ToString();
                ValidateLocalizations(stat.Localizations);
                ValidateNumber(stat.DefaultValue, stat.Type);
                if (stat.MinValue.HasValue)
                    ValidateNumber(stat.MinValue.Value, stat.Type);

                if (stat.MaxValue.HasValue)
                    ValidateNumber(stat.MaxValue.Value, stat.Type);

                if (stat.MaxChange.HasValue)
                {
                    ValidateNumber(stat.MaxChange.Value, stat.Type);
                    if (stat.MaxChange.Value < 0)
                        throw new InvalidDataException($"Stat '{stat.Key}' has a negative maxChange.");
                }

                if ((stat.MinValue > stat.MaxValue) || (stat.DefaultValue < stat.MinValue) || (stat.DefaultValue > stat.MaxValue))
                    throw new InvalidDataException($"Stat '{stat.Key}' has invalid bounds or defaultValue.");

                if (stat.Type == "AVGRATE")
                {
                    if (stat.Window.HasValue && stat.WindowSeconds.HasValue)
                        throw new InvalidDataException($"Stat '{stat.Key}' must use window or windowSeconds, not both.");

                    if (stat.WindowSeconds.HasValue)
                    {
                        if (stat.SecondsPerUnit.HasValue && (stat.SecondsPerUnit.Value != 1d))
                            throw new InvalidDataException("windowSeconds uses secondsPerUnit = 1.");

                        stat.Window = stat.WindowSeconds;
                        stat.WindowSeconds = null;
                    }

                    stat.SecondsPerUnit ??= 1d;
                    if ((stat.Window.HasValue == false) || (stat.Window.Value <= 0) || (stat.SecondsPerUnit.Value <= 0))
                        throw new InvalidDataException($"AVGRATE stat '{stat.Key}' requires a positive window and secondsPerUnit.");

                    ValidateFinite(stat.Window.Value);
                    ValidateFinite(stat.SecondsPerUnit.Value);
                }
                else if ((stat.Type != "INT") && (stat.Type != "FLOAT"))
                {
                    throw new InvalidDataException($"Unsupported stat type '{stat.Type}'.");
                }
                else if ((stat.Window.GetValueOrDefault() != 0) || (stat.WindowSeconds.GetValueOrDefault() != 0) || stat.SecondsPerUnit.HasValue)
                {
                    throw new InvalidDataException("Window and secondsPerUnit apply only to AVGRATE stats.");
                }
            }

            var achievementKeys = new HashSet<string>(StringComparer.Ordinal);
            var achievementIds = new HashSet<int>();
            foreach (var achievement in result.Achievements ?? Array.Empty<Achievement>())
            {
                if (achievement == null)
                    throw new InvalidDataException("Achievement definitions contain an empty item.");

                MyPlatform.EnsureAchievementKey(achievement.Key);
                if (achievementKeys.Add(achievement.Key) == false)
                    throw new InvalidDataException($"Duplicate achievement '{achievement.Key}'.");

                ValidateId(achievement.Id, achievementIds);
                achievement.SetBy = ParseWriter(achievement.SetBy).ToString();
                ValidateLocalizations(achievement.Localizations);
                if ((achievement.DisplayName != null) && (achievement.Title != null) && (achievement.DisplayName != achievement.Title))
                    throw new InvalidDataException("displayName and title disagree.");

                if ((achievement.Description != null) && (achievement.Desc != null) && (achievement.Description != achievement.Desc))
                    throw new InvalidDataException("description and desc disagree.");

                achievement.DisplayName ??= achievement.Title;
                achievement.Description ??= achievement.Desc;
                achievement.Title = null;
                achievement.Desc = null;
                if ((achievement.StatKey != null) != achievement.TargetValue.HasValue)
                    throw new InvalidDataException($"Achievement '{achievement.Key}' requires statKey and targetValue together.");

                if (achievement.StatKey != null)
                {
                    if (stats.TryGetValue(achievement.StatKey, out var stat))
                        ValidateNumber(achievement.TargetValue.Value, stat.Type);
                    else if (requireReferencedStats)
                        throw new InvalidDataException($"Achievement '{achievement.Key}' references undefined stat '{achievement.StatKey}'.");
                    else
                        ValidateFinite(achievement.TargetValue.Value);
                }
            }

            return result;
        }

        public static WriterEnum ParseWriter(string value)
        {
            if (value == null)
                return WriterEnum.Client;

            if (value == "GS")
                return WriterEnum.GameServer;

            if (value == "Official GS")
                return WriterEnum.OfficialGameServer;

            if (Enum.TryParse<WriterEnum>(value, out var writer) && Enum.IsDefined(typeof(WriterEnum), writer) && (writer.ToString() == value))
                return writer;

            throw new InvalidDataException($"Unsupported Set By '{value}'.");
        }

        public string ToJson()
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(MyProgressDefinitions)).WriteObject(stream, this);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public static void ValidateChange(Stat definition, double previousValue, double value)
        {
            ValidateNumber(value, definition.Type);
            if ((value < definition.MinValue) || (value > definition.MaxValue) || (definition.IncrementOnly && (value < previousValue)) || (definition.MaxChange.HasValue && (Math.Abs(value - previousValue) > definition.MaxChange.Value)))
                throw new InvalidOperationException($"Stat '{definition.Key}' violates its configured constraints.");
        }

        private static void ValidateFinite(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidDataException("Progress values must be finite.");
        }

        private static void ValidateId(int? id, HashSet<int> ids)
        {
            if (id.HasValue && ((id.Value < 0) || (ids.Add(id.Value) == false)))
                throw new InvalidDataException("Progress IDs must be nonnegative and unique within each entry type.");
        }

        private static void ValidateLocalizations(LocalizedText[] localizations)
        {
            var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var text in localizations ?? Array.Empty<LocalizedText>())
            {
                if ((text == null) || string.IsNullOrWhiteSpace(text.Language) || (languages.Add(text.Language) == false))
                    throw new InvalidDataException("Localizations require unique, nonempty Steam language names.");
            }
        }

        private static void ValidateNumber(double value, string type)
        {
            ValidateFinite(value);
            if (type == "INT")
            {
                if ((value < int.MinValue) || (value > int.MaxValue) || (value != Math.Truncate(value)))
                    throw new InvalidDataException("An INT value must fit in a signed 32-bit integer.");
            }
            else if (float.IsInfinity((float)value))
            {
                throw new InvalidDataException("A FLOAT or AVGRATE value must fit in a 32-bit float.");
            }
        }
    }
}
