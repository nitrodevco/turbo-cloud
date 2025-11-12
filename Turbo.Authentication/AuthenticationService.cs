using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Primitives.Authentication;

namespace Turbo.Authentication;

public sealed class AuthenticationService(IDbContextFactory<TurboDbContext> dbContextFactory)
    : IAuthenticationService
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;

    public async Task<int> GetPlayerIdFromTicketAsync(string ticket, CancellationToken ct = default)
    {
        if (ticket is null || ticket.Length == 0)
            return 0;

        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            if (dbCtx.SecurityTickets is null)
                return 0;

            var entity = await dbCtx
                .SecurityTickets.AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.Ticket == ticket, ct)
                .ConfigureAwait(false);

            if (entity is null)
                return 0;

            // check timestamp for expiration, if time now is greater than expiration, return 0;

            if (!entity.IsLocked)
            {
                dbCtx.SecurityTickets.Remove(entity);

                await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            return entity.PlayerEntityId;
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }
}
