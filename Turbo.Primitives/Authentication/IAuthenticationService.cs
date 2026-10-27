using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Authentication;

public interface IAuthenticationService
{
    public Task<int> GetPlayerIdFromTicketAsync(string ticket, CancellationToken ct = default);

    /// <summary>
    /// The player a login ticket that has not run out is for, without using it up (0 for none): what
    /// the client asks before it connects, its theme (<c>docs/plugin-data.md</c>), with the ticket it
    /// will log in with.
    /// </summary>
    public Task<int> FindPlayerIdFromTicketAsync(string ticket, CancellationToken ct = default);
}
