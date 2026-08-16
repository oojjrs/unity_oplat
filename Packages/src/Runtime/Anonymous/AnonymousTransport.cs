using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal static class AnonymousTransport
    {
        internal enum FrameTypeEnum : byte
        {
            ControlRequest = 1,
            ControlResponse = 2,
            ControlNotification = 3,
            ClientToHostPacket = 4,
            HostBroadcast = 5,
        }

        internal enum OperationEnum : ushort
        {
            None = 0,
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
            None = 0,
            Success = 1,
            InvalidRequest = 2,
            Unauthenticated = 3,
            NotFound = 4,
            Forbidden = 5,
            Conflict = 6,
            InternalError = 7,
            UnsupportedOperation = 8,
        }

        internal sealed class Frame
        {
            internal Frame(FrameTypeEnum type, long requestId, OperationEnum operation, ResultCodeEnum resultCode, string content)
            {
                Content = content ?? string.Empty;
                Operation = operation;
                RequestId = requestId;
                ResultCode = resultCode;
                Type = type;
            }

            internal string Content { get; }
            internal OperationEnum Operation { get; }
            internal long RequestId { get; }
            internal ResultCodeEnum ResultCode { get; }
            internal FrameTypeEnum Type { get; }
        }

        internal const int Port = 45831;

        private const int FrameHeaderLength = sizeof(uint) + sizeof(ushort) + sizeof(byte) + sizeof(long) + sizeof(ushort) + sizeof(byte);
        private const int FrameLengthPrefix = sizeof(int);
        private const int MaximumContentLength = 1024 * 1024;
        private const uint ProtocolMagic = 0x4F504C54;
        private const ushort ProtocolVersion = 1;

        private static readonly UTF8Encoding Utf8 = new(false, true);

        internal static async Task<T> FromJsonAsync<T>(string content, CancellationToken cancellationToken)
        {
            return await Task.Run(() => JsonUtility.FromJson<T>(content), cancellationToken);
        }

        internal static async Task<string> ToJsonAsync(object content, CancellationToken cancellationToken)
        {
            return await Task.Run(() => JsonUtility.ToJson(content), cancellationToken);
        }

        internal static async Task<Frame> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            var lengthData = new byte[FrameLengthPrefix];
            if (await ReadExactlyAsync(stream, lengthData, true, cancellationToken) == false)
                return null;

            var frameLength = BinaryPrimitives.ReadInt32BigEndian(lengthData);
            if ((frameLength < FrameHeaderLength) || (frameLength > FrameHeaderLength + MaximumContentLength))
                throw new FormatException($"Invalid anonymous frame length: {frameLength}.");

            var frameData = new byte[frameLength];
            await ReadExactlyAsync(stream, frameData, false, cancellationToken);

            var offset = 0;
            var magic = BinaryPrimitives.ReadUInt32BigEndian(frameData.AsSpan(offset, sizeof(uint)));
            offset += sizeof(uint);
            if (magic != ProtocolMagic)
                throw new FormatException("Invalid anonymous protocol magic.");

            var version = BinaryPrimitives.ReadUInt16BigEndian(frameData.AsSpan(offset, sizeof(ushort)));
            offset += sizeof(ushort);
            if (version != ProtocolVersion)
                throw new FormatException($"Unsupported anonymous protocol version: {version}.");

            var type = (FrameTypeEnum)frameData[offset];
            ++offset;
            if (Enum.IsDefined(typeof(FrameTypeEnum), type) == false)
                throw new FormatException($"Invalid anonymous frame type: {type}.");

            var requestId = BinaryPrimitives.ReadInt64BigEndian(frameData.AsSpan(offset, sizeof(long)));
            offset += sizeof(long);
            var operation = (OperationEnum)BinaryPrimitives.ReadUInt16BigEndian(frameData.AsSpan(offset, sizeof(ushort)));
            offset += sizeof(ushort);
            var resultCode = (ResultCodeEnum)frameData[offset];
            ++offset;

            string content;
            try
            {
                content = offset == frameData.Length ? string.Empty : Utf8.GetString(frameData, offset, frameData.Length - offset);
            }
            catch (DecoderFallbackException exception)
            {
                throw new FormatException("Invalid anonymous frame encoding.", exception);
            }

            return new Frame(type, requestId, operation, resultCode, content);
        }

        internal static async Task WriteAsync(Stream stream, Frame frame, CancellationToken cancellationToken)
        {
            var contentData = Utf8.GetBytes(frame.Content);
            if (contentData.Length > MaximumContentLength)
                throw new FormatException($"Anonymous frame content is too large: {contentData.Length}.");

            var frameLength = FrameHeaderLength + contentData.Length;
            var data = new byte[FrameLengthPrefix + frameLength];
            var offset = 0;
            BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset, sizeof(int)), frameLength);
            offset += sizeof(int);
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset, sizeof(uint)), ProtocolMagic);
            offset += sizeof(uint);
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset, sizeof(ushort)), ProtocolVersion);
            offset += sizeof(ushort);
            data[offset] = (byte)frame.Type;
            ++offset;
            BinaryPrimitives.WriteInt64BigEndian(data.AsSpan(offset, sizeof(long)), frame.RequestId);
            offset += sizeof(long);
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset, sizeof(ushort)), (ushort)frame.Operation);
            offset += sizeof(ushort);
            data[offset] = (byte)frame.ResultCode;
            ++offset;
            contentData.CopyTo(data, offset);

            await stream.WriteAsync(data, 0, data.Length, cancellationToken);
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

                    throw new EndOfStreamException("Anonymous connection closed during a frame.");
                }

                offset += readLength;
            }

            return true;
        }
    }
}
