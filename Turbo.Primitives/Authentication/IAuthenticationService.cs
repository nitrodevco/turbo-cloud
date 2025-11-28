using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Authentication;

public interface IAuthenticationService
{
    public Task<int> GetPlayerIdFromTicketAsync(string ticket, CancellationToken ct = default);
}
