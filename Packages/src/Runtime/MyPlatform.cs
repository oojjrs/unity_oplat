using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat
{
    internal static class MyPlatform
    {
        internal interface PlatformInterface : MyPlatformServiceInterface
        {
            Task RunAsync(MyPlatformInitializer.CallbackInterface callback, CancellationToken cancellationToken);
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

        public static MyTimeServiceInterface CreateTimeService(MyTime originTime, bool isSynchronized)
        {
            return new TimeService(originTime, isSynchronized);
        }

        public static MyTimeServiceInterface CreateTimeServiceFromLocalClock()
        {
            return CreateTimeService(MyTime.FromUtcDateTime(DateTime.UtcNow), false);
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
