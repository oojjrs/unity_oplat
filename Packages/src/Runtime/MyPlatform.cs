using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[assembly: InternalsVisibleTo("oojjrs.oplat.anonymous")]

namespace oojjrs.oplat
{
    internal static class MyPlatform
    {
        internal interface PlatformInterface
        {
            Task RunAsync(CancellationToken cancellationToken);
        }

        private static readonly Dictionary<MyPlatformTypeEnum, Func<PlatformInterface>> __platformFactories = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearRegistrations()
        {
            __platformFactories.Clear();
        }

        internal static PlatformInterface CreatePlatform(MyPlatformTypeEnum type)
        {
            if (__platformFactories.TryGetValue(type, out var platformFactory))
                return platformFactory();

            throw new NotImplementedException();
        }

        internal static void Register(MyPlatformTypeEnum type, Func<PlatformInterface> platformFactory)
        {
            __platformFactories.Add(type, platformFactory);
        }
    }
}
