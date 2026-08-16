using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal static class AnonymousTransport
    {
        internal sealed class Message
        {
            internal enum TypeEnum : byte
            {
                Operation = 1,
                OperationResult = 2,
                MemberRequest = 3,
                HostResponse = 4,
            }

            internal byte[] Content { get; }
            internal AnonymousNet.OperationEnum Operation { get; }
            internal AnonymousServerResponse.ResultCodeEnum ResultCode { get; }
            internal TypeEnum Type { get; }

            private Message(TypeEnum type, AnonymousNet.OperationEnum operation, AnonymousServerResponse.ResultCodeEnum resultCode, byte[] content)
            {
                Content = content ?? Array.Empty<byte>();
                Operation = operation;
                ResultCode = resultCode;
                Type = type;
            }

            internal static Message CreateHostResponse(byte[] content)
            {
                return new Message(TypeEnum.HostResponse, default, default, content);
            }

            internal static Message CreateMemberRequest(byte[] content)
            {
                return new Message(TypeEnum.MemberRequest, default, default, content);
            }

            internal static Message CreateOperation(AnonymousNet.OperationEnum operation, byte[] content)
            {
                return new Message(TypeEnum.Operation, operation, default, content);
            }

            internal static Message CreateOperationResult(AnonymousNet.OperationEnum operation, AnonymousServerResponse response)
            {
                return new Message(TypeEnum.OperationResult, operation, response.ResultCode, response.Content);
            }

            internal static Message Deserialize(byte[] data)
            {
                if ((data == null) || (data.Length < 1))
                    throw new FormatException("Invalid anonymous message.");

                var type = (TypeEnum)data[0];
                return type switch
                {
                    TypeEnum.HostResponse => new Message(type, default, default, data[1..]),
                    TypeEnum.MemberRequest => new Message(type, default, default, data[1..]),
                    TypeEnum.Operation when data.Length >= 2 => new Message(type, (AnonymousNet.OperationEnum)data[1], default, data[2..]),
                    TypeEnum.OperationResult when data.Length >= 3 => new Message(type, (AnonymousNet.OperationEnum)data[1], (AnonymousServerResponse.ResultCodeEnum)data[2], data[3..]),
                    _ => throw new FormatException("Invalid anonymous message."),
                };
            }

            internal byte[] Serialize()
            {
                var headerLength = Type switch
                {
                    TypeEnum.HostResponse => 1,
                    TypeEnum.MemberRequest => 1,
                    TypeEnum.Operation => 2,
                    TypeEnum.OperationResult => 3,
                    _ => throw new InvalidOperationException("Invalid anonymous message."),
                };
                var data = new byte[headerLength + Content.Length];
                data[0] = (byte)Type;
                if ((Type == TypeEnum.Operation) || (Type == TypeEnum.OperationResult))
                    data[1] = (byte)Operation;

                if (Type == TypeEnum.OperationResult)
                    data[2] = (byte)ResultCode;

                Content.CopyTo(data, headerLength);
                return data;
            }
        }

        internal sealed class MessageQueue
        {
            private readonly CancellationToken LifetimeCancellationToken;
            private readonly Queue<Message> ReceivedMessages = new();
            private readonly Queue<Message> SendingMessages = new();
            private readonly Stream Stream;

            private Exception _exception;
            private bool _isReceiveCompleted;
            private bool _isSendCompleted;

            internal MessageQueue(Stream stream, CancellationToken lifetimeCancellationToken)
            {
                Stream = stream ?? throw new ArgumentNullException(nameof(stream));
                LifetimeCancellationToken = lifetimeCancellationToken;
                _ = ReadAsync();
                _ = WriteAsync();
            }

            private async Task ReadAsync()
            {
                try
                {
                    while (LifetimeCancellationToken.IsCancellationRequested == false)
                    {
                        var data = await AnonymousTransport.ReadAsync(Stream, LifetimeCancellationToken);
                        if (data == null)
                            return;

                        ReceivedMessages.Enqueue(Message.Deserialize(data));
                    }
                }
                catch (OperationCanceledException) when (LifetimeCancellationToken.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    _exception ??= exception;
                }
                finally
                {
                    _isReceiveCompleted = true;
                }
            }

            internal async Task<Message> ReceiveAsync(Func<Message, bool> predicate, CancellationToken callerCancellationToken)
            {
                if (predicate == null)
                    throw new ArgumentNullException(nameof(predicate));

                using (var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(callerCancellationToken, LifetimeCancellationToken))
                {
                    var cancellationToken = cancellationSource.Token;
                    while (cancellationToken.IsCancellationRequested == false)
                    {
                        if (TryReceive(predicate, out var message))
                            return message;

                        ThrowIfFailed();
                        if (_isReceiveCompleted)
                            return null;

                        await Task.Delay(1, cancellationToken);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    return null;
                }
            }

            internal void Send(Message message)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                ThrowIfFailed();
                if (_isSendCompleted)
                    throw new EndOfStreamException("Anonymous message stream is closed.");

                SendingMessages.Enqueue(message);
            }

            private void ThrowIfFailed()
            {
                if (_exception != null)
                    throw new IOException("Anonymous message stream failed.", _exception);
            }

            private bool TryReceive(Func<Message, bool> predicate, out Message message)
            {
                var count = ReceivedMessages.Count;
                while (count-- > 0)
                {
                    message = ReceivedMessages.Dequeue();
                    if (predicate(message))
                        return true;

                    ReceivedMessages.Enqueue(message);
                }

                message = null;
                return false;
            }

            private async Task WriteAsync()
            {
                try
                {
                    while (LifetimeCancellationToken.IsCancellationRequested == false)
                    {
                        while (SendingMessages.Count > 0)
                        {
                            var message = SendingMessages.Dequeue();
                            await AnonymousTransport.WriteAsync(Stream, message.Serialize(), LifetimeCancellationToken);
                        }

                        await Task.Delay(1, LifetimeCancellationToken);
                    }
                }
                catch (OperationCanceledException) when (LifetimeCancellationToken.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    _exception ??= exception;
                }
                finally
                {
                    _isSendCompleted = true;
                }
            }
        }

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

        internal static Task WriteAsync(Stream stream, byte[] content, CancellationToken cancellationToken)
        {
            var data = new byte[LengthSize + content.Length];
            BinaryPrimitives.WriteInt32BigEndian(data, content.Length);
            content.CopyTo(data, LengthSize);
            return stream.WriteAsync(data, 0, data.Length, cancellationToken);
        }
    }
}
