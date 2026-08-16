using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousClient
    {
        private sealed class PendingRequest
        {
            private int _isWriting;

            internal PendingRequest(AnonymousTransport.OperationEnum operation)
            {
                Operation = operation;
                Source = new TaskCompletionSource<AnonymousServerResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            internal AnonymousTransport.OperationEnum Operation { get; }
            internal TaskCompletionSource<AnonymousServerResponse> Source { get; }

            internal bool IsWriting => Volatile.Read(ref _isWriting) != 0;

            internal void BeginWrite()
            {
                Interlocked.Exchange(ref _isWriting, 1);
            }

            internal void EndWrite()
            {
                Interlocked.Exchange(ref _isWriting, 0);
            }
        }

        private readonly SemaphoreSlim ConnectSemaphore = new(1, 1);
        private readonly CancellationToken LifetimeCancellationToken;
        private readonly ConcurrentDictionary<long, PendingRequest> Requests = new();
        private readonly SemaphoreSlim SendSemaphore = new(1, 1);
        private readonly object StateLock = new();

        private TcpClient _client;
        private bool _isShutdown;
        private long _nextRequestId;
        private NetworkStream _stream;
        private Exception _terminalException;

        internal AnonymousClient(CancellationToken lifetimeCancellationToken)
        {
            LifetimeCancellationToken = lifetimeCancellationToken;
        }

        internal async Task ConnectAsync(CancellationToken cancellationToken)
        {
            await ConnectSemaphore.WaitAsync(cancellationToken);
            try
            {
                lock (StateLock)
                {
                    if (_isShutdown)
                        throw new ObjectDisposedException(nameof(AnonymousClient));

                    if (_stream != null)
                        return;

                    if (_terminalException != null)
                        throw new IOException("Anonymous connection is unavailable.", _terminalException);
                }

                var client = new TcpClient(AddressFamily.InterNetwork);
                try
                {
                    using (cancellationToken.Register(() => client.Close()))
                        await client.ConnectAsync(IPAddress.Loopback, AnonymousTransport.Port);

                    cancellationToken.ThrowIfCancellationRequested();
                    client.NoDelay = true;
                    lock (StateLock)
                    {
                        if (_isShutdown)
                            throw new ObjectDisposedException(nameof(AnonymousClient));

                        _client = client;
                        _stream = client.GetStream();
                    }

                    _ = Task.Run(ReceiveAsync);
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    client.Close();
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }
                catch
                {
                    client.Close();
                    throw;
                }
            }
            finally
            {
                ConnectSemaphore.Release();
            }
        }

        internal async Task<AnonymousServerResponse> SendAsync(AnonymousTransport.OperationEnum operation, object argument, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LifetimeCancellationToken.ThrowIfCancellationRequested();

            var content = argument == null ? string.Empty : await AnonymousTransport.ToJsonAsync(argument, cancellationToken);
            var requestId = Interlocked.Increment(ref _nextRequestId);
            var request = new PendingRequest(operation);
            if (Requests.TryAdd(requestId, request) == false)
                throw new InvalidOperationException($"Anonymous request ID is already pending: {requestId}.");

            using (cancellationToken.Register(() => CancelRequest(requestId)))
            {
                try
                {
                    var frame = new AnonymousTransport.Frame(AnonymousTransport.FrameTypeEnum.ControlRequest, requestId, operation, AnonymousTransport.ResultCodeEnum.None, content);
                    await SendFrameAsync(frame, request, cancellationToken);
                    return await request.Source.Task;
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    Requests.TryRemove(requestId, out _);
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }
                catch (FormatException)
                {
                    Requests.TryRemove(requestId, out _);
                    throw;
                }
                catch (Exception exception)
                {
                    Requests.TryRemove(requestId, out _);
                    FailConnection(exception);
                    throw;
                }
            }
        }

        internal void Shutdown()
        {
            TcpClient client;
            lock (StateLock)
            {
                if (_isShutdown)
                    return;

                _isShutdown = true;
                client = _client;
                _client = null;
                _stream = null;
            }

            client?.Close();
            CompleteResponses(new ObjectDisposedException(nameof(AnonymousClient)));
        }

        private void CancelRequest(long requestId)
        {
            if (Requests.TryRemove(requestId, out var request) == false)
                return;

            request.Source.TrySetCanceled();
            if (request.IsWriting)
                FailConnection(new IOException("Anonymous connection was closed during a canceled frame write."));
        }

        private void CompleteResponses(Exception exception)
        {
            foreach (var pair in Requests)
            {
                if (Requests.TryRemove(pair.Key, out var request))
                    request.Source.TrySetException(exception);
            }
        }

        private void FailConnection(Exception exception)
        {
            TcpClient client;
            lock (StateLock)
            {
                if (_terminalException != null)
                    return;

                _terminalException = exception;
                client = _client;
                _client = null;
                _stream = null;
            }

            client?.Close();
            CompleteResponses(exception);
        }

        private async Task ReceiveAsync()
        {
            try
            {
                NetworkStream stream;
                lock (StateLock)
                    stream = _stream;

                while (LifetimeCancellationToken.IsCancellationRequested == false)
                {
                    var frame = await AnonymousTransport.ReadAsync(stream, LifetimeCancellationToken);
                    if (frame == null)
                        throw new EndOfStreamException("Anonymous server disconnected.");

                    if ((frame.Type != AnonymousTransport.FrameTypeEnum.ControlResponse) || (frame.RequestId < 1) || (frame.ResultCode == AnonymousTransport.ResultCodeEnum.None) || (Enum.IsDefined(typeof(AnonymousTransport.ResultCodeEnum), frame.ResultCode) == false))
                        throw new FormatException("Invalid anonymous control response.");

                    if (Requests.TryRemove(frame.RequestId, out var request))
                    {
                        if (frame.Operation != request.Operation)
                        {
                            var exception = new FormatException("Anonymous control response operation does not match its request.");
                            request.Source.TrySetException(exception);
                            throw exception;
                        }

                        request.Source.TrySetResult(new AnonymousServerResponse(frame.ResultCode, frame.Content));
                    }
                }
            }
            catch (OperationCanceledException) when (LifetimeCancellationToken.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (LifetimeCancellationToken.IsCancellationRequested || _isShutdown)
            {
            }
            catch (Exception exception)
            {
                FailConnection(exception);
            }
        }

        private async Task SendFrameAsync(AnonymousTransport.Frame frame, PendingRequest request, CancellationToken requestCancellationToken)
        {
            await SendSemaphore.WaitAsync(requestCancellationToken);
            try
            {
                requestCancellationToken.ThrowIfCancellationRequested();
                LifetimeCancellationToken.ThrowIfCancellationRequested();

                NetworkStream stream;
                lock (StateLock)
                {
                    if (_terminalException != null)
                        throw new IOException("Anonymous connection is unavailable.", _terminalException);

                    stream = _stream ?? throw new InvalidOperationException("Anonymous connection is not established.");
                }

                request.BeginWrite();
                try
                {
                    requestCancellationToken.ThrowIfCancellationRequested();
                    await AnonymousTransport.WriteAsync(stream, frame, LifetimeCancellationToken);
                }
                finally
                {
                    request.EndWrite();
                }
            }
            finally
            {
                SendSemaphore.Release();
            }
        }
    }
}
