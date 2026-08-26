using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudSave;

namespace oojjrs.oplat.ugsymous
{
    internal sealed class UgsymousStorage : MyStorageServiceInterface
    {
        private const int HeaderByteCount = 8;
        private const int Magic = 0x4F504C54;
        private const string KeyPrefix = "oplat_";

        int MyStorageServiceInterface.FileByteCountMax => MyStorage.FileByteCountMax;

        async Task<bool> MyStorageServiceInterface.DeleteAsync(string fileName, CancellationToken cancellationToken)
        {
            MyStorage.EnsureFileName(fileName);
            if (await ExistsAsync(fileName, cancellationToken) == false)
                return false;

            await CloudSaveService.Instance.Files.Player.DeleteAsync(ToKey(fileName));
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }

        Task<bool> MyStorageServiceInterface.ExistsAsync(string fileName, CancellationToken cancellationToken) => ExistsAsync(fileName, cancellationToken);

        async Task<IReadOnlyList<MyStorageServiceInterface.FileInfo>> MyStorageServiceInterface.ListAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var items = await CloudSaveService.Instance.Files.Player.ListAllAsync();
            var result = new List<MyStorageServiceInterface.FileInfo>();
            foreach (var item in items.Where(t => t.Key.StartsWith(KeyPrefix, StringComparison.Ordinal)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var envelope = await CloudSaveService.Instance.Files.Player.LoadBytesAsync(item.Key);
                if (TryReadEnvelope(envelope, out var fileName, out var data))
                    result.Add(new(fileName, item.Modified?.ToUniversalTime() ?? DateTime.MinValue, data.LongLength));
            }

            return result.OrderBy(t => t.FileName, StringComparer.Ordinal).ToArray();
        }

        async Task<MyStorageServiceInterface.ReadResult> MyStorageServiceInterface.ReadAsync(string fileName, CancellationToken cancellationToken)
        {
            MyStorage.EnsureFileName(fileName);
            if (await ExistsAsync(fileName, cancellationToken) == false)
                return new(false, null);

            var envelope = await CloudSaveService.Instance.Files.Player.LoadBytesAsync(ToKey(fileName));
            cancellationToken.ThrowIfCancellationRequested();
            return TryReadEnvelope(envelope, out var storedName, out var data) && (storedName == fileName) ? new(true, data) : new(false, null);
        }

        async Task MyStorageServiceInterface.WriteAsync(string fileName, byte[] data, CancellationToken cancellationToken)
        {
            MyStorage.EnsureFileName(fileName);
            MyStorage.EnsureData(data);
            cancellationToken.ThrowIfCancellationRequested();
            await CloudSaveService.Instance.Files.Player.SaveAsync(ToKey(fileName), ToEnvelope(fileName, data));
            cancellationToken.ThrowIfCancellationRequested();
        }

        private static async Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken)
        {
            MyStorage.EnsureFileName(fileName);
            cancellationToken.ThrowIfCancellationRequested();
            var key = ToKey(fileName);
            var items = await CloudSaveService.Instance.Files.Player.ListAllAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return items.Any(t => t.Key == key);
        }

        private static byte[] ToEnvelope(string fileName, byte[] data)
        {
            var name = Encoding.UTF8.GetBytes(fileName);
            var result = new byte[HeaderByteCount + name.Length + data.Length];
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(0, 4), Magic);
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(4, 4), name.Length);
            name.CopyTo(result, HeaderByteCount);
            data.CopyTo(result, HeaderByteCount + name.Length);
            return result;
        }

        private static string ToKey(string fileName)
        {
            using var sha256 = SHA256.Create();
            return KeyPrefix + UgsymousPlatform.ToHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(fileName)));
        }

        private static bool TryReadEnvelope(byte[] envelope, out string fileName, out byte[] data)
        {
            fileName = null;
            data = null;
            if ((envelope == null) || (envelope.Length < HeaderByteCount) || (BinaryPrimitives.ReadInt32LittleEndian(envelope.AsSpan(0, 4)) != Magic))
                return false;

            var nameLength = BinaryPrimitives.ReadInt32LittleEndian(envelope.AsSpan(4, 4));
            if ((nameLength < 0) || (HeaderByteCount + nameLength > envelope.Length))
                return false;

            fileName = Encoding.UTF8.GetString(envelope, HeaderByteCount, nameLength);
            data = envelope[(HeaderByteCount + nameLength)..];
            return true;
        }
    }
}
