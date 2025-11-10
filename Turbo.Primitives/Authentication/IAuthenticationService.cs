using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Primitives.Authentication;

public interface IAuthenticationService
{
    public Task<int> GetPlayerIdFromTicketAsync(string ticket, CancellationToken ct = default);
    public Task AssociateSessionWithPlayerAsync(SessionKey key, long playerId);
}
