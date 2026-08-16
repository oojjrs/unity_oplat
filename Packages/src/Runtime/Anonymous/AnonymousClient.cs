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
                await _client.ConnectAsync(IPAddress.Loopback, AnonymousTransport.Port);

            cancellationToken.ThrowIfCancellationRequested();
            _client.NoDelay = true;
            _stream = _client.GetStream();
        }

        internal async Task<AnonymousServerResponse> SendAsync(AnonymousTransport.OperationEnum operation, byte[] content, CancellationToken cancellationToken)
        {
            await AnonymousTransport.WriteRequestAsync(_stream, operation, content, cancellationToken);
            return await AnonymousTransport.ReadResponseAsync(_stream, cancellationToken);
        }

        internal void Shutdown()
        {
            _client?.Close();
            _client = null;
            _stream = null;
        }
    }
}
