using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousClient
    {
        private TcpClient _client;
        private NetworkStream _stream;

        internal async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (_stream != null)
                return;

            _client = new TcpClient(AddressFamily.InterNetwork);
            using (cancellationToken.Register(_client.Close))
                await _client.ConnectAsync(IPAddress.Loopback, AnonymousNet.Port);

            cancellationToken.ThrowIfCancellationRequested();
            _client.NoDelay = true;
            _stream = _client.GetStream();
        }

        internal async Task<AnonymousServerResponse> SendAndReceiveAsync(AnonymousNet.OperationEnum operation, byte[] content, CancellationToken cancellationToken)
        {
            var request = new byte[1 + content.Length];
            request[0] = (byte)operation;
            content.CopyTo(request, 1);
            await AnonymousTransport.WriteAsync(_stream, request, cancellationToken);

            var response = await AnonymousTransport.ReadAsync(_stream, cancellationToken);
            return new AnonymousServerResponse((AnonymousServerResponse.ResultCodeEnum)response[0], response[1..]);
        }

        internal void Shutdown()
        {
            _client?.Close();
            _client = null;
            _stream = null;
        }
    }
}
