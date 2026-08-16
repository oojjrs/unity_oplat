using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal static class AnonymousTransport
    {
        private const int LengthSize = sizeof(int);

        internal static async Task<byte[]> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            var length = new byte[LengthSize];
            if (await ReadExactlyAsync(stream, length, true, cancellationToken) == false)
                return null;

            var content = new byte[BinaryPrimitives.ReadInt32BigEndian(length)];
            await ReadExactlyAsync(stream, content, false, cancellationToken);
            return content;
        }

        internal static Task WriteAsync(Stream stream, byte[] content, CancellationToken cancellationToken)
        {
            var data = new byte[LengthSize + content.Length];
            BinaryPrimitives.WriteInt32BigEndian(data, content.Length);
            content.CopyTo(data, LengthSize);
            return stream.WriteAsync(data, 0, data.Length, cancellationToken);
        }

        private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] content, bool allowEmpty, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < content.Length)
            {
                var length = await stream.ReadAsync(content, offset, content.Length - offset, cancellationToken);
                if (length == 0)
                {
                    if (allowEmpty && offset == 0)
                        return false;

                    throw new EndOfStreamException();
                }

                offset += length;
            }

            return true;
        }
    }
}
