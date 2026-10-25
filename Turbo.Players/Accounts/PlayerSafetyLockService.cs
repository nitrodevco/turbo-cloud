using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Accounts;

namespace Turbo.Players.Accounts;

/// <summary>
/// <see cref="IPlayerSafetyLockService"/>, kept in the player's <c>safety_locked</c> column. A
/// change reaches an online client as <c>AccountSafetyLockStatusChange</c>; an offline player reads
/// it in <c>UserObject</c> at their next login.
/// </summary>
public sealed class PlayerSafetyLockService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory
) : IPlayerSafetyLockService
{
    public async Task<bool> IsLockedAsync(PlayerId playerId, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        return await db
            .Players.AsNoTracking()
            .Where(x => x.Id == playerId.Value)
            .Select(x => x.SafetyLocked)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> SetLockedAsync(PlayerId playerId, bool locked, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var changed = await db
            .Players.Where(x => x.Id == playerId.Value && x.SafetyLocked != locked)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.SafetyLocked, locked), ct)
            .ConfigureAwait(false);

        if (changed == 0)
            return false;

        await grainFactory
            .TrySendComposerToPlayerAsync(
                playerId,
                new AccountSafetyLockStatusChangeMessageComposer
                {
                    Status = locked
                        ? AccountSafetyLockStatusChangeMessageComposer.LOCKED
                        : AccountSafetyLockStatusChangeMessageComposer.UNLOCKED,
                },
                ct
            )
            .ConfigureAwait(false);

        return true;
    }
}
