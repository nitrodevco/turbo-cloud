using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Moderation;
using Turbo.Database.Extensions;
using Turbo.Events;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Moderation.Events;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Operations;

/// <summary>
/// Hotel bans, read and written through the database. A login asks it, so the lookup is one
/// indexed query for a player who has never been sanctioned. A ban given or lifted raises
/// <see cref="PlayerSanctionChangedEvent"/>.
/// </summary>
public sealed class SanctionService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    TimeProvider timeProvider,
    EventSystem eventSystem,
    ILogger<ISanctionService> logger
) : ISanctionService
{
    public async Task<PlayerSanctionSnapshot?> GetActiveBanAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var entity = await ActiveBans(dbCtx, playerId, now)
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

        return entity?.ToSnapshot();
    }

    public async Task<PlayerSanctionSnapshot> BanAsync(
        PlayerId playerId,
        DateTime? expiresAtUtc,
        string reason,
        PlayerId? issuer,
        CancellationToken ct
    )
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        // One ban in force at a time: the new one replaces what is running, so lifting it is one
        // step and "when does this player get back in" has one answer.
        await RevokeActiveBansAsync(dbCtx, playerId, issuer, now, ct);

        var entity = new PlayerSanctionEntity
        {
            PlayerEntityId = playerId.Value,
            Kind = SanctionKind.Ban,
            Reason =
                reason.Length <= PlayerSanctionEntity.REASON_MAX_LENGTH
                    ? reason
                    : reason[..PlayerSanctionEntity.REASON_MAX_LENGTH],
            IssuerEntityId = issuer?.Value,
            ExpiresAt = expiresAtUtc,
            // The clock this service reads, not the database's, so the issue time and the expiry
            // are measured by the same one.
            CreatedAt = now,
        };

        dbCtx.PlayerSanctions.Add(entity);

        await dbCtx.SaveChangesAsync(ct);
        Announce(playerId);

        return entity.ToSnapshot();
    }

    public async Task<bool> UnbanAsync(PlayerId playerId, PlayerId? revokedBy, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        if (await RevokeActiveBansAsync(dbCtx, playerId, revokedBy, now, ct) == 0)
            return false;

        Announce(playerId);

        return true;
    }

    /// <summary>Not awaited: a handler cannot hold up the ban, nor fail it once it is written.</summary>
    private void Announce(PlayerId playerId) =>
        eventSystem
            .PublishAsync(
                new PlayerSanctionChangedEvent { PlayerId = playerId },
                CancellationToken.None
            )
            .LogAndForget(logger, "announce the sanctions of player {PlayerId}", playerId);

    private static Task<int> RevokeActiveBansAsync(
        TurboDbContext dbCtx,
        PlayerId playerId,
        PlayerId? revokedBy,
        DateTime now,
        CancellationToken ct
    )
    {
        int? revokedById = revokedBy?.Value;

        return ActiveBans(dbCtx, playerId, now)
            .ExecuteUpdateAsync(
                set =>
                    set.SetProperty(x => x.RevokedAt, now)
                        .SetProperty(x => x.RevokedByEntityId, revokedById),
                ct
            );
    }

    private static IQueryable<PlayerSanctionEntity> ActiveBans(
        TurboDbContext dbCtx,
        PlayerId playerId,
        DateTime now
    ) =>
        dbCtx.PlayerSanctions.Where(x =>
            x.PlayerEntityId == playerId.Value
            && x.Kind == SanctionKind.Ban
            && x.RevokedAt == null
            && (x.ExpiresAt == null || x.ExpiresAt > now)
        );
}
