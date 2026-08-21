using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat
{
    public interface MyStorageServiceInterface
    {
        public readonly struct FileInfo
        {
            internal FileInfo(string fileName, DateTime lastWriteTimeUtc, long sizeBytes)
            {
                FileName = fileName;
                LastWriteTimeUtc = lastWriteTimeUtc;
                SizeBytes = sizeBytes;
            }

            public string FileName { get; }
            public DateTime LastWriteTimeUtc { get; }
            public long SizeBytes { get; }
        }

        public readonly struct ReadResult
        {
            private readonly byte[] DataValue;

            internal ReadResult(bool isFound, byte[] data)
            {
                DataValue = data ?? Array.Empty<byte>();
                IsFound = isFound;
            }

            public byte[] Data => DataValue ?? Array.Empty<byte>();
            public bool IsFound { get; }
        }

        int FileByteCountMax { get; }

        Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken);
        Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken);
        Task<IReadOnlyList<FileInfo>> ListAsync(CancellationToken cancellationToken);
        Task<ReadResult> ReadAsync(string fileName, CancellationToken cancellationToken);
        Task WriteAsync(string fileName, byte[] data, CancellationToken cancellationToken);
    }
}
