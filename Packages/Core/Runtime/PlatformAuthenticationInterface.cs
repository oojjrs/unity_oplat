using System.Threading;
using System.Threading.Tasks;

namespace oojjrs.oplat
{
    public interface PlatformAuthenticationInterface
    {
        Task<PlatformAuthenticationTicket> CreateTicketAsync(CancellationToken cancellationToken);
    }
}
