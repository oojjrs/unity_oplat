using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousServerConnection
    {
        private readonly TcpClient Client;
        private readonly SemaphoreSlim SendSemaphore = new(1, 1);
        private readonly NetworkStream Stream;

        internal AnonymousServerConnection(TcpClient client)
        {
            Client = client;
            Stream = client.GetStream();
        }

        internal AnonymousServerSession Session { get; set; }

        internal Task<AnonymousTransport.Frame> ReadAsync(CancellationToken cancellationToken)
        {
            return AnonymousTransport.ReadAsync(Stream, cancellationToken);
        }

        internal async Task SendAsync(AnonymousTransport.Frame frame, CancellationToken cancellationToken)
        {
            await SendSemaphore.WaitAsync(cancellationToken);
            try
            {
                await AnonymousTransport.WriteAsync(Stream, frame, cancellationToken);
            }
            finally
            {
                SendSemaphore.Release();
            }
        }

        internal void Shutdown()
        {
            Client.Close();
        }
    }
}
