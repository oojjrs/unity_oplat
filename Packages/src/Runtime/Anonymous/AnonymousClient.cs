using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousClient
    {
        private readonly CancellationToken LifetimeCancellationToken;

        private TcpClient _client;
        private AnonymousTransport.MessageQueue _messages;

        internal AnonymousClient(CancellationToken lifetimeCancellationToken)
        {
            LifetimeCancellationToken = lifetimeCancellationToken;
        }

        internal async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (_messages != null)
                return;

            _client = new TcpClient(AddressFamily.InterNetwork);
            using (cancellationToken.Register(_client.Close))
                await _client.ConnectAsync(IPAddress.Loopback, AnonymousNet.Port);

            cancellationToken.ThrowIfCancellationRequested();
            _client.NoDelay = true;
            _messages = new AnonymousTransport.MessageQueue(_client.GetStream(), LifetimeCancellationToken);
        }

        internal async Task<AnonymousServerResponse> ReceiveAsync(AnonymousNet.OperationEnum operation, CancellationToken cancellationToken)
        {
            if (_messages == null)
                throw new InvalidOperationException("Anonymous client is not connected.");

            var message = await _messages.ReceiveAsync(value => (value.Type == AnonymousTransport.Message.TypeEnum.OperationResult) && (value.Operation == operation), cancellationToken);
            if (message == null)
                throw new EndOfStreamException("Anonymous server disconnected.");

            return new AnonymousServerResponse(message.ResultCode, message.Content);
        }

        internal void Send(AnonymousNet.OperationEnum operation, byte[] content)
        {
            if (_messages == null)
                throw new InvalidOperationException("Anonymous client is not connected.");

            _messages.Send(AnonymousTransport.Message.CreateOperation(operation, content));
        }

        internal void Shutdown()
        {
            _client?.Close();
            _client = null;
            _messages = null;
        }
    }
}
