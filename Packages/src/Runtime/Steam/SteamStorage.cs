#if STEAMWORKS_NET
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;

namespace oojjrs.oplat.steam
{
    internal class SteamStorage : MyStorageServiceInterface
    {
        private static readonly byte[] EmptyNativeData = new byte[1];

        private bool _isInitialized;
        private CancellationTokenSource _lifetimeSource;
        private int _mainThreadId;

        int MyStorageServiceInterface.FileByteCountMax => MyStorage.FileByteCountMax;

        private static byte[] CreateDataSnapshot(byte[] data)
        {
            var snapshot = new byte[data.Length];
            Buffer.BlockCopy(data, 0, snapshot, 0, data.Length);
            return snapshot;
        }

        async Task<bool> MyStorageServiceInterface.DeleteAsync(string fileName, CancellationToken callerCancellationToken)
        {
            EnsureInitialized();
            MyStorage.EnsureFileName(fileName);
            using (var cancellationSource = CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                cancellationToken.ThrowIfCancellationRequested();
                if (SteamRemoteStorage.FileExists(fileName) == false)
                    return await Task.FromResult(false);

                cancellationToken.ThrowIfCancellationRequested();
                if (SteamRemoteStorage.FileDelete(fileName) == false)
                    throw new InvalidOperationException($"Steam failed to delete storage file '{fileName}'.");

                return await Task.FromResult(true);
            }
        }

        async Task<bool> MyStorageServiceInterface.ExistsAsync(string fileName, CancellationToken callerCancellationToken)
        {
            EnsureInitialized();
            MyStorage.EnsureFileName(fileName);
            using (var cancellationSource = CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                cancellationToken.ThrowIfCancellationRequested();
                return await Task.FromResult(SteamRemoteStorage.FileExists(fileName));
            }
        }

        async Task<IReadOnlyList<MyStorageServiceInterface.FileInfo>> MyStorageServiceInterface.ListAsync(CancellationToken callerCancellationToken)
        {
            EnsureInitialized();
            using (var cancellationSource = CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                cancellationToken.ThrowIfCancellationRequested();
                var fileCount = SteamRemoteStorage.GetFileCount();
                if (fileCount < 0)
                    throw new InvalidOperationException("Steam returned an invalid storage file count.");

                var files = new List<MyStorageServiceInterface.FileInfo>(fileCount);
                for (var index = 0; index < fileCount; ++index)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileName = SteamRemoteStorage.GetFileNameAndSize(index, out var sizeBytes);
                    if (string.IsNullOrEmpty(fileName))
                        continue;

                    if (sizeBytes < 0)
                        throw new InvalidOperationException($"Steam returned an invalid size for storage file '{fileName}'.");

                    var lastWriteTimeUtc = DateTimeOffset.FromUnixTimeSeconds(SteamRemoteStorage.GetFileTimestamp(fileName)).UtcDateTime;
                    files.Add(new MyStorageServiceInterface.FileInfo(fileName, lastWriteTimeUtc, sizeBytes));
                }

                cancellationToken.ThrowIfCancellationRequested();
                files.Sort((left, right) => string.Compare(left.FileName, right.FileName, StringComparison.Ordinal));
                return await Task.FromResult<IReadOnlyList<MyStorageServiceInterface.FileInfo>>(files.ToArray());
            }
        }

        async Task<MyStorageServiceInterface.ReadResult> MyStorageServiceInterface.ReadAsync(string fileName, CancellationToken callerCancellationToken)
        {
            EnsureInitialized();
            MyStorage.EnsureFileName(fileName);
            using (var cancellationSource = CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                cancellationToken.ThrowIfCancellationRequested();
                if (SteamRemoteStorage.FileExists(fileName) == false)
                    return new MyStorageServiceInterface.ReadResult(false, Array.Empty<byte>());

                var fileByteCount = SteamRemoteStorage.GetFileSize(fileName);
                if (fileByteCount < 0)
                    throw new InvalidOperationException($"Steam returned an invalid size for storage file '{fileName}'.");

                if (fileByteCount > MyStorage.FileByteCountMax)
                    throw new InvalidOperationException($"Steam storage file '{fileName}' exceeds the {MyStorage.FileByteCountMax}-byte limit.");

                if (fileByteCount == 0)
                    return new MyStorageServiceInterface.ReadResult(true, Array.Empty<byte>());

                cancellationToken.ThrowIfCancellationRequested();
                var lifetimeCancellationToken = _lifetimeSource.Token;
                var apiCall = SteamRemoteStorage.FileReadAsync(fileName, 0, checked((uint)fileByteCount));
                if (apiCall == SteamAPICall_t.Invalid)
                    throw new InvalidOperationException($"Steam rejected the storage read request for '{fileName}'.");

                var data = await WaitForReadAsync(apiCall, checked((uint)fileByteCount), lifetimeCancellationToken);
                return new MyStorageServiceInterface.ReadResult(true, data);
            }
        }

        async Task MyStorageServiceInterface.WriteAsync(string fileName, byte[] data, CancellationToken callerCancellationToken)
        {
            EnsureInitialized();
            MyStorage.EnsureFileName(fileName);
            MyStorage.EnsureData(data);
            callerCancellationToken.ThrowIfCancellationRequested();
            var snapshot = CreateDataSnapshot(data);
            using (var cancellationSource = CreateCancellationSource(callerCancellationToken))
            {
                var cancellationToken = cancellationSource.Token;
                cancellationToken.ThrowIfCancellationRequested();
                var lifetimeCancellationToken = _lifetimeSource.Token;
                var nativeData = snapshot.Length == 0 ? EmptyNativeData : snapshot;
                var apiCall = SteamRemoteStorage.FileWriteAsync(fileName, nativeData, checked((uint)snapshot.Length));
                if (apiCall == SteamAPICall_t.Invalid)
                    throw new InvalidOperationException($"Steam rejected the storage write request for '{fileName}'. The file name, file size, or Steam Cloud quota may be invalid.");

                await WaitForWriteAsync(apiCall, lifetimeCancellationToken);
            }
        }

        internal void Initialize()
        {
            if (_isInitialized)
                return;

            _mainThreadId = Environment.CurrentManagedThreadId;
            _lifetimeSource = new CancellationTokenSource();
            _isInitialized = true;
        }

        internal void Shutdown()
        {
            if (_isInitialized == false)
                return;

            EnsureMainThread();
            _isInitialized = false;
            _lifetimeSource.Cancel();
            _lifetimeSource.Dispose();
            _lifetimeSource = null;
        }

        private CancellationTokenSource CreateCancellationSource(CancellationToken callerCancellationToken)
        {
            EnsureInitialized();
            return CancellationTokenSource.CreateLinkedTokenSource(callerCancellationToken, _lifetimeSource.Token);
        }

        private void EnsureInitialized()
        {
            if (_isInitialized == false)
                throw new InvalidOperationException("Steam storage is not initialized.");

            EnsureMainThread();
        }

        private void EnsureMainThread()
        {
            if (Environment.CurrentManagedThreadId != _mainThreadId)
                throw new InvalidOperationException("Steam storage operations must run on the Unity main thread.");
        }

        private async Task<byte[]> WaitForReadAsync(SteamAPICall_t apiCall, uint requestedByteCount, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var callResult = CallResult<RemoteStorageFileReadAsyncComplete_t>.Create((callback, ioFailure) =>
            {
                try
                {
                    if (ioFailure)
                        throw new InvalidOperationException("Steam failed while reading a storage file.");

                    if (callback.m_eResult != EResult.k_EResultOK)
                        throw new InvalidOperationException($"Steam storage read failed ({callback.m_eResult}).");

                    if (callback.m_hFileReadAsync != apiCall)
                        throw new InvalidOperationException("Steam returned a mismatched storage read handle.");

                    if ((callback.m_nOffset != 0) || (callback.m_cubRead > requestedByteCount))
                        throw new InvalidOperationException("Steam returned invalid storage read bounds.");

                    var data = callback.m_cubRead == 0 ? Array.Empty<byte>() : new byte[checked((int)callback.m_cubRead)];
                    var nativeData = data.Length == 0 ? EmptyNativeData : data;
                    if (SteamRemoteStorage.FileReadAsyncComplete(callback.m_hFileReadAsync, nativeData, callback.m_cubRead) == false)
                        throw new InvalidOperationException("Steam failed to copy the completed storage read.");

                    source.TrySetResult(data);
                }
                catch (Exception exception)
                {
                    source.TrySetException(exception);
                }
            }))
            {
                callResult.Set(apiCall);
                using (cancellationToken.Register(() => source.TrySetCanceled()))
                    return await source.Task;
            }
        }

        private async Task WaitForWriteAsync(SteamAPICall_t apiCall, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var callResult = CallResult<RemoteStorageFileWriteAsyncComplete_t>.Create((callback, ioFailure) =>
            {
                if (ioFailure)
                {
                    source.TrySetException(new InvalidOperationException("Steam failed while writing a storage file."));
                    return;
                }

                if (callback.m_eResult != EResult.k_EResultOK)
                {
                    source.TrySetException(new InvalidOperationException($"Steam storage write failed ({callback.m_eResult})."));
                    return;
                }

                source.TrySetResult(true);
            }))
            {
                callResult.Set(apiCall);
                using (cancellationToken.Register(() => source.TrySetCanceled()))
                    await source.Task;
            }
        }
    }
}
#endif
