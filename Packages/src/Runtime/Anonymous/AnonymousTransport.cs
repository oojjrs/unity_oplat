using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal static class AnonymousTransport
    {
        internal enum OperationEnum : byte
        {
            Authenticate = 1,
            CreateRoom = 2,
            ExitRoom = 3,
            GetRooms = 4,
            JoinRoom = 5,
            UpdatePlayer = 6,
            UpdateRoom = 7,
        }

        internal enum ResultCodeEnum : byte
        {
            Success = 1,
            Unauthenticated = 2,
            NotFound = 3,
            Forbidden = 4,
            Conflict = 5,
            UnsupportedOperation = 6,
        }

        internal const int Port = 45831;

        private const int LengthSize = sizeof(int);

        internal static Task<T> DeserializeAsync<T>(byte[] content)
        {
            return Task.Run(() =>
            {
                using (var stream = new MemoryStream(content))
                    return (T)MyNetDeserializer.Deserialize(stream);
            });
        }

        internal static async Task<(OperationEnum Operation, byte[] Content)?> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
        {
            var data = await ReadAsync(stream, true, cancellationToken);
            if (data == null)
                return null;

            return ((OperationEnum)data[0], data[1..]);
        }

        internal static async Task<AnonymousServerResponse> ReadResponseAsync(Stream stream, CancellationToken cancellationToken)
        {
            var data = await ReadAsync(stream, false, cancellationToken);
            return new AnonymousServerResponse((ResultCodeEnum)data[0], data[1..]);
        }

        internal static Task<byte[]> SerializeAsync(object content)
        {
            return Task.Run(() => content == null ? Array.Empty<byte>() : MyNetSerializer.Serialize(content));
        }

        internal static Task WriteRequestAsync(Stream stream, OperationEnum operation, byte[] content, CancellationToken cancellationToken)
        {
            return WriteAsync(stream, (byte)operation, content, cancellationToken);
        }

        internal static Task WriteResponseAsync(Stream stream, AnonymousServerResponse response, CancellationToken cancellationToken)
        {
            return WriteAsync(stream, (byte)response.ResultCode, response.Content, cancellationToken);
        }

        private static async Task<byte[]> ReadAsync(Stream stream, bool allowEmpty, CancellationToken cancellationToken)
        {
            var lengthData = new byte[LengthSize];
            if (await ReadExactlyAsync(stream, lengthData, allowEmpty, cancellationToken) == false)
                return null;

            var data = new byte[BinaryPrimitives.ReadInt32BigEndian(lengthData)];
            await ReadExactlyAsync(stream, data, false, cancellationToken);
            return data;
        }

        private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] data, bool allowEmpty, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < data.Length)
            {
                var readLength = await stream.ReadAsync(data, offset, data.Length - offset, cancellationToken);
                if (readLength == 0)
                {
                    if (allowEmpty && offset == 0)
                        return false;

                    throw new EndOfStreamException();
                }

                offset += readLength;
            }

            return true;
        }

        private static Task WriteAsync(Stream stream, byte header, byte[] content, CancellationToken cancellationToken)
        {
            var data = new byte[LengthSize + 1 + content.Length];
            BinaryPrimitives.WriteInt32BigEndian(data, data.Length - LengthSize);
            data[LengthSize] = header;
            content.CopyTo(data, LengthSize + 1);
            return stream.WriteAsync(data, 0, data.Length, cancellationToken);
        }
    }
}
