using System;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace oojjrs.oplat.steam
{
    internal sealed class SteamPlatform : MonoBehaviour, MyPlatform.PlatformInterface
    {
        private bool _isInitialized;

        private void OnDestroy()
        {
            if (_isInitialized == false)
                return;

            SteamAPI.Shutdown();
        }

        private void Update()
        {
            if (_isInitialized)
                SteamAPI.RunCallbacks();
        }

        Task MyPlatform.PlatformInterface.RunAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_isInitialized)
                return Task.CompletedTask;

            var result = SteamAPI.InitEx(out var errorMessage);
            if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                throw new InvalidOperationException($"Steam initialization failed ({result}): {errorMessage}");

            _isInitialized = true;

            return Task.CompletedTask;
        }
    }
}
