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
            cancellationToken.ThrowIfCancellationRequested();
            if (message == null)
                throw new EndOfStreamException("Anonymous server disconnected.");

            return new AnonymousServerResponse(message.ResultCode, message.Content);
        }

        internal void Send(AnonymousNet.OperationEnum operation, byte[] content)
        {
            SendMessage(AnonymousTransport.Message.CreateOperation(operation, content));
        }

        internal void SendHostResponse(byte[] content)
        {
            SendMessage(AnonymousTransport.Message.CreateHostResponse(content));
        }

        internal void SendMemberRequest(byte[] content)
        {
            SendMessage(AnonymousTransport.Message.CreateMemberRequest(content));
        }

        internal bool TryReceiveHostResponse(out byte[] content)
        {
            if ((_messages != null) && _messages.TryReceive(message => message.Type == AnonymousTransport.Message.TypeEnum.HostResponse, out var message))
            {
                content = message.Content;
                return true;
            }

            content = null;
            return false;
        }

        internal bool TryReceiveMemberRequest(out byte[] content)
        {
            if ((_messages != null) && _messages.TryReceive(message => message.Type == AnonymousTransport.Message.TypeEnum.MemberRequest, out var message))
            {
                content = message.Content;
                return true;
            }

            content = null;
            return false;
        }

        internal bool TryReceivePlayerUpdated(out byte[] content)
        {
            if ((_messages != null) && _messages.TryReceive(message => message.Type == AnonymousTransport.Message.TypeEnum.PlayerUpdated, out var message))
            {
                content = message.Content;
                return true;
            }

            content = null;
            return false;
        }

        internal bool TryReceiveRoomUpdated(out byte[] content)
        {
            if ((_messages != null) && _messages.TryReceive(message => message.Type == AnonymousTransport.Message.TypeEnum.RoomUpdated, out var message))
            {
                content = message.Content;
                return true;
            }

            content = null;
            return false;
        }

        private void SendMessage(AnonymousTransport.Message message)
        {
            if (_messages == null)
                throw new InvalidOperationException("Anonymous client is not connected.");

            _messages.Send(message);
        }

        internal void Shutdown()
        {
            _client?.Close();
            _client = null;
            _messages = null;
        }
    }
}
