using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat
{
    public sealed class ChunkedMyStorageService : MyStorageServiceInterface
    {
        private sealed class Manifest
        {
            public string FileName { get; }
            public string Generation { get; }
            public byte[] Hash { get; }
            public int PartByteCount { get; }
            public int PartCount => 1 + (SizeBytes - 1) / PartByteCount;
            public int SizeBytes { get; }

            public Manifest(string fileName, string generation, byte[] hash, int partByteCount, int sizeBytes)
            {
                FileName = fileName;
                Generation = generation;
                Hash = hash;
                PartByteCount = partByteCount;
                SizeBytes = sizeBytes;
            }
        }

        private sealed class Operation : IDisposable
        {
            private readonly string _fileName;
            private readonly OperationGate _gate;
            private readonly ChunkedMyStorageService _owner;

            public Operation(ChunkedMyStorageService owner, string fileName, OperationGate gate)
            {
                _owner = owner;
                _fileName = fileName;
                _gate = gate;
            }

            void IDisposable.Dispose()
            {
                _gate.Semaphore.Release();
                _owner.ReleaseGate(_fileName, _gate);
            }
        }

        private sealed class OperationGate
        {
            public SemaphoreSlim Semaphore { get; } = new(1, 1);
            public int Users { get; set; }
        }

        private const int HashByteCount = 32;
        private const int ManifestByteCountMax = 1024;
        private const string ManifestMagic = "OPLATCF1";
        private const string ManifestSuffix = "manifest.bin";
        private const string ReservedPrefix = "__oplat_storage_chunks_v1/";

        private readonly MyStorageServiceInterface _backend;
        private readonly Dictionary<string, OperationGate> _operations = new(StringComparer.Ordinal);

        int MyStorageServiceInterface.FileByteCountMax => int.MaxValue;

        public ChunkedMyStorageService(MyStorageServiceInterface backend)
        {
            if (backend == null)
                throw new ArgumentNullException(nameof(backend));
            if (backend.FileByteCountMax < ManifestByteCountMax)
                throw new ArgumentOutOfRangeException(nameof(backend), "The storage backend cannot hold a file manifest.");
            _backend = backend;
        }

        private static byte[] CreateHash(byte[] data)
        {
            using (var hash = SHA256.Create())
                return hash.ComputeHash(data);
        }

        private static void EnsureFileName(string fileName)
        {
            MyStorage.EnsureFileName(fileName);
            if (fileName.StartsWith(ReservedPrefix, StringComparison.OrdinalIgnoreCase) || string.Equals(fileName, ReservedPrefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The storage file name uses the reserved chunk directory.", nameof(fileName));
        }

        private static string GetFilePrefix(string fileName)
        {
            return ReservedPrefix + BitConverter.ToString(CreateHash(Encoding.UTF8.GetBytes(fileName))).Replace("-", string.Empty).ToLowerInvariant() + "/";
        }

        private static string GetManifestName(string fileName)
        {
            return GetFilePrefix(fileName) + ManifestSuffix;
        }

        private static string GetPartName(Manifest manifest, int index)
        {
            return GetFilePrefix(manifest.FileName) + manifest.Generation + "/" + index.ToString("D3", CultureInfo.InvariantCulture) + ".part";
        }

        private static bool HashesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
                return false;
            for (var index = 0; index < left.Length; ++index)
            {
                if (left[index] != right[index])
                    return false;
            }
            return true;
        }

        private static byte[] SerializeManifest(Manifest manifest)
        {
            using (var output = new MemoryStream())
            {
                using (var writer = new BinaryWriter(output, Encoding.UTF8, true))
                {
                    writer.Write(ManifestMagic);
                    writer.Write(manifest.FileName);
                    writer.Write(manifest.Generation);
                    writer.Write(manifest.PartByteCount);
                    writer.Write(manifest.SizeBytes);
                    writer.Write(manifest.Hash);
                }
                return output.ToArray();
            }
        }

        async Task<bool> MyStorageServiceInterface.DeleteAsync(string fileName, CancellationToken cancellationToken)
        {
            EnsureFileName(fileName);
            using (await EnterAsync(fileName, cancellationToken))
            {
                var manifestName = GetManifestName(fileName);
                var hasManifest = await _backend.ExistsAsync(manifestName, cancellationToken);
                var hasFile = await _backend.DeleteAsync(fileName, cancellationToken);
                if (hasManifest)
                    await _backend.DeleteAsync(manifestName, CancellationToken.None);
                await TryCleanupAsync(fileName);
                return hasManifest || hasFile;
            }
        }

        async Task<bool> MyStorageServiceInterface.ExistsAsync(string fileName, CancellationToken cancellationToken)
        {
            EnsureFileName(fileName);
            using (await EnterAsync(fileName, cancellationToken))
            {
                if (await _backend.ExistsAsync(GetManifestName(fileName), cancellationToken))
                    return true;
                return await _backend.ExistsAsync(fileName, cancellationToken);
            }
        }

        async Task<IReadOnlyList<MyStorageServiceInterface.FileInfo>> MyStorageServiceInterface.ListAsync(CancellationToken cancellationToken)
        {
            var physicalFiles = await _backend.ListAsync(cancellationToken);
            var files = new Dictionary<string, MyStorageServiceInterface.FileInfo>(StringComparer.Ordinal);
            foreach (var file in physicalFiles)
            {
                if (file.FileName.StartsWith(ReservedPrefix, StringComparison.Ordinal) == false)
                    files.Add(file.FileName, file);
            }
            foreach (var file in physicalFiles)
            {
                if (file.FileName.StartsWith(ReservedPrefix, StringComparison.Ordinal) && file.FileName.EndsWith("/" + ManifestSuffix, StringComparison.Ordinal))
                {
                    var manifest = await ReadManifestAsync(file.FileName, cancellationToken);
                    if (manifest != null)
                        files[manifest.FileName] = new MyStorageServiceInterface.FileInfo(manifest.FileName, file.LastWriteTimeUtc, manifest.SizeBytes);
                }
            }
            var snapshot = new List<MyStorageServiceInterface.FileInfo>(files.Values);
            snapshot.Sort((left, right) => string.Compare(left.FileName, right.FileName, StringComparison.Ordinal));
            return snapshot.ToArray();
        }

        async Task<MyStorageServiceInterface.ReadResult> MyStorageServiceInterface.ReadAsync(string fileName, CancellationToken cancellationToken)
        {
            EnsureFileName(fileName);
            using (await EnterAsync(fileName, cancellationToken))
            {
                var manifest = await ReadManifestAsync(GetManifestName(fileName), cancellationToken);
                if (manifest == null)
                    return await _backend.ReadAsync(fileName, cancellationToken);
                var data = new byte[manifest.SizeBytes];
                for (var index = 0; index < manifest.PartCount; ++index)
                {
                    var part = await _backend.ReadAsync(GetPartName(manifest, index), cancellationToken);
                    var offset = checked(index * manifest.PartByteCount);
                    var byteCount = Math.Min(manifest.PartByteCount, data.Length - offset);
                    if ((part.IsFound == false) || (part.Data.Length != byteCount))
                        throw new InvalidDataException($"Storage file '{fileName}' has a missing or incomplete part ({index}).");
                    Buffer.BlockCopy(part.Data, 0, data, offset, byteCount);
                }
                if (HashesEqual(CreateHash(data), manifest.Hash) == false)
                    throw new InvalidDataException($"Storage file '{fileName}' failed its integrity check.");
                return new MyStorageServiceInterface.ReadResult(true, data);
            }
        }

        async Task MyStorageServiceInterface.WriteAsync(string fileName, byte[] data, CancellationToken cancellationToken)
        {
            EnsureFileName(fileName);
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = new byte[data.Length];
            Buffer.BlockCopy(data, 0, snapshot, 0, data.Length);
            using (await EnterAsync(fileName, cancellationToken))
            {
                await CleanupAsync(fileName, cancellationToken);
                if (snapshot.Length <= _backend.FileByteCountMax)
                {
                    await _backend.WriteAsync(fileName, snapshot, cancellationToken);
                    await _backend.DeleteAsync(GetManifestName(fileName), CancellationToken.None);
                    await TryCleanupAsync(fileName);
                    return;
                }
                var manifest = new Manifest(fileName, Guid.NewGuid().ToString("N"), CreateHash(snapshot), _backend.FileByteCountMax, snapshot.Length);
                try
                {
                    for (var index = 0; index < manifest.PartCount; ++index)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var offset = checked(index * manifest.PartByteCount);
                        var part = new byte[Math.Min(manifest.PartByteCount, snapshot.Length - offset)];
                        Buffer.BlockCopy(snapshot, offset, part, 0, part.Length);
                        await _backend.WriteAsync(GetPartName(manifest, index), part, cancellationToken);
                    }
                    await _backend.WriteAsync(GetManifestName(fileName), SerializeManifest(manifest), cancellationToken);
                }
                catch
                {
                    await TryCleanupAsync(fileName);
                    throw;
                }
                await TryCleanupAsync(fileName);
            }
        }

        private async Task CleanupAsync(string fileName, CancellationToken cancellationToken)
        {
            var filePrefix = GetFilePrefix(fileName);
            var manifestName = filePrefix + ManifestSuffix;
            var manifest = await ReadManifestAsync(manifestName, cancellationToken);
            var livePrefix = manifest == null ? null : filePrefix + manifest.Generation + "/";
            foreach (var file in await _backend.ListAsync(cancellationToken))
            {
                if (file.FileName.StartsWith(filePrefix, StringComparison.Ordinal) && (file.FileName != manifestName))
                {
                    if ((livePrefix == null) || (file.FileName.StartsWith(livePrefix, StringComparison.Ordinal) == false))
                        await _backend.DeleteAsync(file.FileName, cancellationToken);
                }
            }
            if (manifest != null)
                await _backend.DeleteAsync(fileName, cancellationToken);
        }

        private async Task<Operation> EnterAsync(string fileName, CancellationToken cancellationToken)
        {
            OperationGate gate;
            lock (_operations)
            {
                if (_operations.TryGetValue(fileName, out gate) == false)
                {
                    gate = new();
                    _operations.Add(fileName, gate);
                }
                ++gate.Users;
            }
            try
            {
                await gate.Semaphore.WaitAsync(cancellationToken);
                return new Operation(this, fileName, gate);
            }
            catch
            {
                ReleaseGate(fileName, gate);
                throw;
            }
        }

        private async Task<Manifest> ReadManifestAsync(string manifestName, CancellationToken cancellationToken)
        {
            var result = await _backend.ReadAsync(manifestName, cancellationToken);
            if (result.IsFound == false)
                return null;
            if (result.Data.Length > ManifestByteCountMax)
                throw new InvalidDataException("The storage file manifest is too large.");
            try
            {
                using (var input = new MemoryStream(result.Data, false))
                {
                    using (var reader = new BinaryReader(input, Encoding.UTF8, true))
                    {
                        if (reader.ReadString() != ManifestMagic)
                            throw new InvalidDataException("Unknown storage file manifest format.");
                        var fileName = reader.ReadString();
                        EnsureFileName(fileName);
                        var generation = reader.ReadString();
                        var partByteCount = reader.ReadInt32();
                        var sizeBytes = reader.ReadInt32();
                        var hash = reader.ReadBytes(HashByteCount);
                        if ((GetManifestName(fileName) != manifestName) || (Guid.TryParseExact(generation, "N", out _) == false) || (partByteCount != _backend.FileByteCountMax) || (sizeBytes <= partByteCount) || (hash.Length != HashByteCount) || (input.Position != input.Length))
                            throw new InvalidDataException("The storage file manifest contains invalid bounds or identity.");
                        return new Manifest(fileName, generation, hash, partByteCount, sizeBytes);
                    }
                }
            }
            catch (EndOfStreamException exception)
            {
                throw new InvalidDataException("The storage file manifest is incomplete.", exception);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("The storage file manifest contains an invalid file name.", exception);
            }
        }

        private void ReleaseGate(string fileName, OperationGate gate)
        {
            lock (_operations)
            {
                if (--gate.Users == 0)
                {
                    _operations.Remove(fileName);
                    gate.Semaphore.Dispose();
                }
            }
        }

        private async Task TryCleanupAsync(string fileName)
        {
            try
            {
                await CleanupAsync(fileName, CancellationToken.None);
            }
            catch (Exception)
            {
            }
        }
    }
}
