using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Security;
using Turbo.Primitives.Authentication;

namespace Turbo.Authentication;

/// <summary>
/// The player a login ticket is for. A ticket that has run out is refused and removed; one that is
/// not reusable is removed by the login that uses it, and only one login can use it.
/// </summary>
public sealed class AuthenticationService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    TimeProvider timeProvider
) : IAuthenticationService
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;

    public async Task<int> FindPlayerIdFromTicketAsync(
        string ticket,
        CancellationToken ct = default
    )
    {
        if (ticket is null || ticket.Length == 0)
            return 0;

        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        if (dbCtx.SecurityTickets is null)
            return 0;

        var hashed = SecurityTicketEntity.Hash(ticket);
        var asWritten = ticket.StartsWith(
            SecurityTicketEntity.HASH_PREFIX,
            StringComparison.Ordinal
        )
            ? hashed
            : ticket;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        return await dbCtx
            .SecurityTickets.AsNoTracking()
            .Where(x =>
                (x.Ticket == hashed || x.Ticket == asWritten)
                && (x.ExpiresAt == null || x.ExpiresAt > now)
            )
            .Select(x => x.PlayerEntityId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<int> GetPlayerIdFromTicketAsync(string ticket, CancellationToken ct = default)
    {
        if (ticket is null || ticket.Length == 0)
            return 0;

        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            if (dbCtx.SecurityTickets is null)
                return 0;

            // An issued ticket is stored as its hash; one written by something else (a CMS, the
            // load-test bots) as written. A value shaped like a stored hash is only ever hashed,
            // so a copy of the table can't be replayed.
            var hashed = SecurityTicketEntity.Hash(ticket);
            var asWritten = ticket.StartsWith(
                SecurityTicketEntity.HASH_PREFIX,
                StringComparison.Ordinal
            )
                ? hashed
                : ticket;

            var entity = await dbCtx
                .SecurityTickets.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Ticket == hashed || x.Ticket == asWritten, ct)
                .ConfigureAwait(false);

            if (entity is null)
                return 0;

            if (entity.ExpiresAt is { } expires && expires <= timeProvider.GetUtcNow().UtcDateTime)
            {
                await dbCtx
                    .SecurityTickets.Where(x => x.Id == entity.Id)
                    .ExecuteDeleteAsync(ct)
                    .ConfigureAwait(false);

                return 0;
            }

            // Used up by this login: whichever of two logins with it removes it gets in.
            if (
                !entity.IsLocked
                && await dbCtx
                    .SecurityTickets.Where(x => x.Id == entity.Id)
                    .ExecuteDeleteAsync(ct)
                    .ConfigureAwait(false) == 0
            )
                return 0;

            return entity.PlayerEntityId;
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }
}
