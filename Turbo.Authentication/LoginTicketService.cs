using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Security;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Players;

namespace Turbo.Authentication;

/// <summary>
/// <see cref="ILoginTicketService"/> over <c>security_tickets</c>, one row a player. A ticket is
/// 32 random bytes as hex, so it can't be guessed; it is not bound to an address.
/// </summary>
public sealed class LoginTicketService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    TimeProvider timeProvider
) : ILoginTicketService
{
    private const int TICKET_BYTES = 32;

    public async Task<LoginTicketIssued> IssueAsync(
        PlayerId player,
        TimeSpan? lifetime,
        bool reusable,
        CancellationToken ct
    )
    {
        var ticket = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(TICKET_BYTES));
        DateTime? expires = lifetime is { } span
            ? timeProvider.GetUtcNow().UtcDateTime.Add(span)
            : null;

        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var row = await db
            .SecurityTickets.FirstOrDefaultAsync(x => x.PlayerEntityId == player.Value, ct)
            .ConfigureAwait(false);

        // One ticket a player: the one they had stops working as this one is written.
        if (row is null)
        {
            db.SecurityTickets.Add(
                new SecurityTicketEntity
                {
                    PlayerEntityId = player.Value,
                    Ticket = ticket,
                    IpAddress = string.Empty,
                    IsLocked = reusable,
                    ExpiresAt = expires,
                    PlayerEntity = null!,
                }
            );
        }
        else
        {
            row.Ticket = ticket;
            row.IpAddress = string.Empty;
            row.IsLocked = reusable;
            row.ExpiresAt = expires;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new LoginTicketIssued(ticket, expires, reusable);
    }

    public async Task<LoginTicketStatus?> GetAsync(PlayerId player, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var row = await db
            .SecurityTickets.AsNoTracking()
            .Where(x => x.PlayerEntityId == player.Value)
            .Select(x => new { x.ExpiresAt, x.IsLocked })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return row is null
            ? null
            : new LoginTicketStatus(
                row.ExpiresAt,
                row.IsLocked,
                row.ExpiresAt is { } at && at <= timeProvider.GetUtcNow().UtcDateTime
            );
    }

    public async Task<bool> RevokeAsync(PlayerId player, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        return await db
                .SecurityTickets.Where(x => x.PlayerEntityId == player.Value)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false) > 0;
    }
}
