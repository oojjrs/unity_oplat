using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using oojjrs.oplat;

public static class ChunkedStorageChecks
{
    private sealed class MemoryMyStorageService : MyStorageServiceInterface
    {
        private readonly int _fileByteCountMax;

        public Func<string, Task> BeforeWriteAsync { get; set; }
        public Func<string, bool> DeleteFailure { get; set; }
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        public int FileCountMax { get; set; } = int.MaxValue;
        public int MaximumWriteBytes { get; private set; }
        public long QuotaBytes { get; set; } = long.MaxValue;
        public Func<string, bool> WriteFailure { get; set; }

        int MyStorageServiceInterface.FileByteCountMax => _fileByteCountMax;

        public MemoryMyStorageService(int fileByteCountMax)
        {
            _fileByteCountMax = fileByteCountMax;
        }

        Task<bool> MyStorageServiceInterface.DeleteAsync(string fileName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((DeleteFailure != null) && DeleteFailure(fileName))
                throw new IOException("Injected delete failure.");
            return Task.FromResult(Files.Remove(fileName));
        }

        Task<bool> MyStorageServiceInterface.ExistsAsync(string fileName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Files.ContainsKey(fileName));
        }

        Task<IReadOnlyList<MyStorageServiceInterface.FileInfo>> MyStorageServiceInterface.ListAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<MyStorageServiceInterface.FileInfo>>(Files.Select(file => new MyStorageServiceInterface.FileInfo(file.Key, DateTime.UtcNow, file.Value.Length)).ToArray());
        }

        Task<MyStorageServiceInterface.ReadResult> MyStorageServiceInterface.ReadAsync(string fileName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Files.TryGetValue(fileName, out var data) ? new MyStorageServiceInterface.ReadResult(true, (byte[])data.Clone()) : new MyStorageServiceInterface.ReadResult(false, Array.Empty<byte>()));
        }

        async Task MyStorageServiceInterface.WriteAsync(string fileName, byte[] data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (data.Length > _fileByteCountMax)
                throw new ArgumentOutOfRangeException(nameof(data), "Native write exceeded the physical file limit.");
            MaximumWriteBytes = Math.Max(MaximumWriteBytes, data.Length);
            var snapshot = (byte[])data.Clone();
            if (BeforeWriteAsync != null)
                await BeforeWriteAsync(fileName);
            if ((WriteFailure != null) && WriteFailure(fileName))
                throw new IOException("Injected write failure.");
            var oldSize = Files.TryGetValue(fileName, out var previous) ? previous.Length : 0;
            if ((Files.Values.Sum(file => (long)file.Length) - oldSize + snapshot.Length > QuotaBytes) || ((Files.ContainsKey(fileName) == false) && (Files.Count >= FileCountMax)))
                throw new IOException("Steam Cloud quota or file count exceeded.");
            Files[fileName] = snapshot;
        }
    }

    private const int NativeByteCountMax = 100 * 1024 * 1024;
    private const int SmallByteCountMax = 4096;

    private static async Task AssertThrowsAsync<T>(Func<Task> operation) where T : Exception
    {
        try
        {
            await operation();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private static void Check(bool result, string message)
    {
        if (result == false)
            throw new InvalidOperationException(message);
    }

    private static async Task CleanupRecoveryAsync()
    {
        var backend = new MemoryMyStorageService(SmallByteCountMax);
        MyStorageServiceInterface storage = new ChunkedMyStorageService(backend);
        await storage.WriteAsync("save.bin", CreateData(SmallByteCountMax + 1, 1), CancellationToken.None);
        backend.DeleteFailure = fileName => fileName.EndsWith(".part", StringComparison.Ordinal);
        await storage.WriteAsync("save.bin", CreateData(SmallByteCountMax + 2, 2), CancellationToken.None);
        Check(backend.Files.Count == 5, "Cleanup failure did not retain the obsolete generation for retry.");
        backend.DeleteFailure = null;
        MyStorageServiceInterface restarted = new ChunkedMyStorageService(backend);
        var data = CreateData(SmallByteCountMax + 3, 3);
        await restarted.WriteAsync("save.bin", data, CancellationToken.None);
        Check((backend.Files.Count == 3) && (await restarted.ReadAsync("save.bin", CancellationToken.None)).Data.AsSpan().SequenceEqual(data), "Restart did not recover orphan cleanup.");
    }

    private static async Task CorruptPartsAsync()
    {
        for (var corruption = 0; corruption < 3; ++corruption)
        {
            var backend = new MemoryMyStorageService(SmallByteCountMax);
            MyStorageServiceInterface storage = new ChunkedMyStorageService(backend);
            await storage.WriteAsync("save.bin", CreateData(SmallByteCountMax + 7, 1), CancellationToken.None);
            var partName = backend.Files.Keys.First(name => name.EndsWith(".part", StringComparison.Ordinal));
            if (corruption == 0)
                backend.Files.Remove(partName);
            else if (corruption == 1)
                backend.Files[partName] = new byte[1];
            else
                backend.Files[partName][0] ^= 1;
            await AssertThrowsAsync<InvalidDataException>(async () => await storage.ReadAsync("save.bin", CancellationToken.None));
        }
    }

    private static byte[] CreateData(int length, int seed)
    {
        var data = new byte[length];
        for (var index = 0; index < data.Length; ++index)
            data[index] = (byte)((index * 31 + seed * 17) ^ (index >> 8));
        return data;
    }

    private static async Task DeleteAndListingAsync()
    {
        var backend = new MemoryMyStorageService(SmallByteCountMax);
        MyStorageServiceInterface storage = new ChunkedMyStorageService(backend);
        var large = CreateData(SmallByteCountMax * 3 + 1, 1);
        Check(await storage.ExistsAsync("large.bin", CancellationToken.None) == false, "Missing file reported as existing.");
        await storage.WriteAsync("large.bin", large, CancellationToken.None);
        await storage.WriteAsync("small.bin", new byte[] { 7 }, CancellationToken.None);
        var files = await storage.ListAsync(CancellationToken.None);
        Check((files.Count == 2) && (files[0].FileName == "large.bin") && (files[0].SizeBytes == large.Length) && (files[1].FileName == "small.bin"), "Logical list leaked physical files or returned the wrong size.");
        Check(await storage.ExistsAsync("large.bin", CancellationToken.None), "Chunked file was not found.");
        Check(await storage.DeleteAsync("large.bin", CancellationToken.None), "Chunked file deletion failed.");
        Check((await storage.ReadAsync("large.bin", CancellationToken.None)).IsFound == false, "Deleted chunked file was still readable.");
        Check((backend.Files.Count == 1) && backend.Files.ContainsKey("small.bin"), "Chunked deletion left fragments or removed another file.");
        Check(await storage.DeleteAsync("large.bin", CancellationToken.None) == false, "Missing deletion reported success.");
    }

    private static async Task ExactBoundaryAsync()
    {
        var backend = new MemoryMyStorageService(NativeByteCountMax);
        MyStorageServiceInterface storage = new ChunkedMyStorageService(backend);
        var data = CreateData(NativeByteCountMax, 1);
        await storage.WriteAsync("boundary.bin", data, CancellationToken.None);
        Check((backend.Files.Count == 1) && backend.Files.ContainsKey("boundary.bin"), "Exact 100 MiB boundary was not stored as a normal file.");
        Check((await storage.ReadAsync("boundary.bin", CancellationToken.None)).Data.AsSpan().SequenceEqual(data), "Boundary bytes changed.");
    }

    private static async Task FailedPublicationAsync()
    {
        foreach (var isExistingLarge in new[] { false, true })
        {
            var backend = new MemoryMyStorageService(SmallByteCountMax);
            MyStorageServiceInterface storage = new ChunkedMyStorageService(backend);
            var previous = CreateData(isExistingLarge ? SmallByteCountMax + 1 : 17, 1);
            await storage.WriteAsync("save.bin", previous, CancellationToken.None);
            var count = backend.Files.Count;
            backend.WriteFailure = fileName => fileName.EndsWith("/manifest.bin", StringComparison.Ordinal);
            await AssertThrowsAsync<IOException>(() => storage.WriteAsync("save.bin", CreateData(SmallByteCountMax * 2 + 1, 2), CancellationToken.None));
            Check((await storage.ReadAsync("save.bin", CancellationToken.None)).Data.AsSpan().SequenceEqual(previous), "Failed manifest publication replaced the previous file.");
            Check(backend.Files.Count == count, "Failed publication left new parts behind.");
        }
    }

    private static async Task InterruptedPartsAsync()
    {
        foreach (var isCancellation in new[] { false, true })
        {
            var backend = new MemoryMyStorageService(SmallByteCountMax);
            MyStorageServiceInterface storage = new ChunkedMyStorageService(backend);
            var previous = CreateData(SmallByteCountMax + 1, 1);
            await storage.WriteAsync("save.bin", previous, CancellationToken.None);
            var count = backend.Files.Count;
            using (var cancellation = new CancellationTokenSource())
            {
                if (isCancellation)
                {
                    backend.BeforeWriteAsync = fileName =>
                    {
                        if (fileName.EndsWith("000.part", StringComparison.Ordinal))
                            cancellation.Cancel();
                        return Task.CompletedTask;
                    };
                    await AssertThrowsAsync<OperationCanceledException>(() => storage.WriteAsync("save.bin", CreateData(SmallByteCountMax * 2 + 1, 2), cancellation.Token));
                }
                else
                {
                    backend.WriteFailure = fileName => fileName.EndsWith("001.part", StringComparison.Ordinal);
                    await AssertThrowsAsync<IOException>(() => storage.WriteAsync("save.bin", CreateData(SmallByteCountMax * 2 + 1, 2), CancellationToken.None));
                }
            }
            Check((await storage.ReadAsync("save.bin", CancellationToken.None)).Data.AsSpan().SequenceEqual(previous), "Interrupted part upload damaged the previous file.");
            Check(backend.Files.Count == count, "Interrupted upload left new parts behind.");
        }
    }

    public static async Task<int> Main()
    {
        var checks = new (string Name, Func<Task> Execute)[]
        {
            ("100 MiB exact boundary", ExactBoundaryAsync),
            ("100 MiB + 1 byte round trip", OverBoundaryAsync),
            ("logical list, exists and deletion", DeleteAndListingAsync),
            ("small/large/empty overwrite and restart", OverwriteAsync),
            ("part failure and cancellation", InterruptedPartsAsync),
            ("manifest publication failure", FailedPublicationAsync),
            ("quota and file-count exhaustion", QuotaAsync),
            ("missing, truncated and corrupt parts", CorruptPartsAsync),
            ("corrupt manifest and reserved names", ManifestAndNamesAsync),
            ("same-file concurrency and snapshots", SnapshotAndConcurrencyAsync),
            ("cleanup failure and restart recovery", CleanupRecoveryAsync),
        };
        try
        {
            foreach (var check in checks)
            {
                await check.Execute();
                Console.WriteLine("PASS " + check.Name);
                GC.Collect();
            }
#if STEAMWORKS_NET
            Check(new oojjrs.oplat.steam.SteamStorage().Files.FileByteCountMax == int.MaxValue, "Steam platform does not expose the logical storage limit.");
            Console.WriteLine("PASS actual Steamworks.NET assembly compilation and Steam storage wiring");
#endif
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static async Task ManifestAndNamesAsync()
    {
        var backend = new MemoryMyStorageService(SmallByteCountMax);
        MyStorageServiceInterface storage = new ChunkedMyStorageService(backend);
        await storage.WriteAsync("save.bin", CreateData(SmallByteCountMax + 1, 1), CancellationToken.None);
        var manifestName = backend.Files.Keys.Single(name => name.EndsWith("/manifest.bin", StringComparison.Ordinal));
        backend.Files[manifestName] = new byte[] { 1 };
        await AssertThrowsAsync<InvalidDataException>(async () => await storage.ReadAsync("save.bin", CancellationToken.None));
        Check(await storage.DeleteAsync("save.bin", CancellationToken.None), "Corrupt manifest could not be deleted.");
        Check(backend.Files.Count == 0, "Corrupt manifest deletion left physical parts.");
        await AssertThrowsAsync<ArgumentException>(() => storage.WriteAsync("__oplat_storage_chunks_v1/x", new byte[] { 1 }, CancellationToken.None));
        await AssertThrowsAsync<ArgumentException>(async () => await storage.ReadAsync("__oplat_storage_chunks_v1", CancellationToken.None));
        await AssertThrowsAsync<ArgumentException>(() => storage.WriteAsync("../save.bin", new byte[] { 1 }, CancellationToken.None));
        await AssertThrowsAsync<ArgumentNullException>(() => storage.WriteAsync("save.bin", null, CancellationToken.None));
    }

    private static async Task OverBoundaryAsync()
    {
        var backend = new MemoryMyStorageService(NativeByteCountMax);
        MyStorageServiceInterface storage = new ChunkedMyStorageService(backend);
        var data = CreateData(NativeByteCountMax + 1, 2);
        await storage.WriteAsync("save/player.bin", data, CancellationToken.None);
        Check((backend.MaximumWriteBytes <= NativeByteCountMax) && (backend.Files.Count == 3), "Large write violated Steam's limit or did not publish two parts plus a manifest.");
        MyStorageServiceInterface restarted = new ChunkedMyStorageService(backend);
        Check((await restarted.ReadAsync("save/player.bin", CancellationToken.None)).Data.AsSpan().SequenceEqual(data), "Reassembled large bytes differ from the original.");
        Check((await restarted.ListAsync(CancellationToken.None)).Single().SizeBytes == data.Length, "Large logical size is incorrect.");
    }

    private static async Task OverwriteAsync()
    {
        var backend = new MemoryMyStorageService(SmallByteCountMax);
        MyStorageServiceInterface storage = new ChunkedMyStorageService(backend);
        foreach (var length in new[] { 9, SmallByteCountMax + 1, SmallByteCountMax * 3, 1, SmallByteCountMax + 2, 0 })
        {
            var data = CreateData(length, length);
            await storage.WriteAsync("save.bin", data, CancellationToken.None);
            MyStorageServiceInterface restarted = new ChunkedMyStorageService(backend);
            Check((await restarted.ReadAsync("save.bin", CancellationToken.None)).Data.AsSpan().SequenceEqual(data), "Overwrite did not return the latest bytes.");
            Check(backend.Files.Count == (length <= SmallByteCountMax ? 1 : (length - 1) / SmallByteCountMax + 2), "Overwrite left obsolete physical files.");
        }
        Check((await storage.ReadAsync("save.bin", CancellationToken.None)).IsFound, "Empty file was confused with a missing file.");
    }

    private static async Task QuotaAsync()
    {
        foreach (var isFileCount in new[] { false, true })
        {
            var backend = new MemoryMyStorageService(SmallByteCountMax);
            MyStorageServiceInterface storage = new ChunkedMyStorageService(backend);
            var previous = CreateData(19, 1);
            await storage.WriteAsync("save.bin", previous, CancellationToken.None);
            if (isFileCount)
                backend.FileCountMax = 2;
            else
                backend.QuotaBytes = SmallByteCountMax + previous.Length;
            await AssertThrowsAsync<IOException>(() => storage.WriteAsync("save.bin", CreateData(SmallByteCountMax * 2, 2), CancellationToken.None));
            Check((await storage.ReadAsync("save.bin", CancellationToken.None)).Data.AsSpan().SequenceEqual(previous), "Quota failure replaced the old file.");
            Check((backend.Files.Count == 1) && backend.Files.ContainsKey("save.bin"), "Quota failure leaked physical parts.");
        }
    }

    private static async Task SnapshotAndConcurrencyAsync()
    {
        var backend = new MemoryMyStorageService(SmallByteCountMax);
        MyStorageServiceInterface storage = new ChunkedMyStorageService(backend);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.BeforeWriteAsync = async fileName =>
        {
            if (entered.TrySetResult(true))
                await released.Task;
        };
        var first = CreateData(SmallByteCountMax + 1, 1);
        var expected = (byte[])first.Clone();
        var writing = storage.WriteAsync("save.bin", first, CancellationToken.None);
        await entered.Task;
        Array.Clear(first, 0, first.Length);
        using (var cancellation = new CancellationTokenSource())
        {
            var cancelled = storage.ReadAsync("save.bin", cancellation.Token);
            cancellation.Cancel();
            await AssertThrowsAsync<OperationCanceledException>(async () => await cancelled);
        }
        var reading = storage.ReadAsync("save.bin", CancellationToken.None);
        Check(reading.IsCompleted == false, "Read observed a partially published write.");
        released.SetResult(true);
        await writing;
        Check((await reading).Data.AsSpan().SequenceEqual(expected), "Caller mutation changed the saved snapshot.");
        await storage.WriteAsync("save.bin", new byte[] { 3 }, CancellationToken.None);
        Check((await storage.ReadAsync("save.bin", CancellationToken.None)).Data.SequenceEqual(new byte[] { 3 }), "Cancelled waiter left the file locked.");
    }
}
