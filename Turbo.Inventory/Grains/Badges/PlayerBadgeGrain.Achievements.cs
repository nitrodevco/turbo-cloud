using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Achievements;
using Turbo.Database.Entities.Players;

namespace Turbo.Inventory.Grains.Badges;

internal sealed partial class PlayerBadgeGrain
{
    public async Task GrantAchievementAsync(
        int achievementId,
        int level,
        string badgeCode,
        CancellationToken ct
    )
    {
        if (achievementId <= 0 || level <= 0 || !TryNormalizeCode(badgeCode, out var code))
            throw new ArgumentException("Invalid achievement badge entitlement.");
        await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);
        var entitlement = await db.AchievementBadgeEntitlements.FindAsync(
            [_state.PlayerId.Value, achievementId],
            ct
        );
        if (entitlement is not null && entitlement.Level > level)
            return;
        if (entitlement is not null && entitlement.Level == level && entitlement.BadgeCode != code)
            throw new InvalidOperationException("Frozen award badge mismatch.");
        var oldCode = entitlement?.BadgeCode;
        var oldBadge = oldCode is null
            ? null
            : await db.PlayerBadges.SingleOrDefaultAsync(
                x => x.PlayerEntityId == _state.PlayerId.Value && x.BadgeCode == oldCode,
                ct
            );
        var newBadge = await db.PlayerBadges.SingleOrDefaultAsync(
            x => x.PlayerEntityId == _state.PlayerId.Value && x.BadgeCode == code,
            ct
        );
        var removeOld =
            oldBadge is not null
            && oldCode != code
            && !oldBadge.ManualGrant
            && !await db.AchievementBadgeEntitlements.AnyAsync(
                x =>
                    x.PlayerId == _state.PlayerId.Value
                    && x.AchievementId != achievementId
                    && x.BadgeCode == oldCode,
                ct
            );
        if (newBadge is null)
        {
            newBadge = new PlayerBadgeEntity
            {
                PlayerEntityId = _state.PlayerId.Value,
                BadgeCode = code,
                SlotId = removeOld ? oldBadge!.SlotId : null,
                ManualGrant = false,
                PlayerEntity = null!,
            };
            db.PlayerBadges.Add(newBadge);
        }
        else if (removeOld && oldBadge!.SlotId is > 0 && newBadge.SlotId is null or 0)
            newBadge.SlotId = oldBadge.SlotId;
        if (entitlement is null)
        {
            entitlement = new AchievementBadgeEntitlementEntity
            {
                PlayerId = _state.PlayerId.Value,
                AchievementId = achievementId,
                Level = level,
                BadgeCode = code,
            };
            db.AchievementBadgeEntitlements.Add(entitlement);
        }
        else
        {
            entitlement.Level = level;
            entitlement.BadgeCode = code;
        }
        if (removeOld)
            db.PlayerBadges.Remove(oldBadge!);
        await db.SaveChangesAsync(ct);
        // Hydration and directory recount are replayable even if a previous send failed after commit.
        await HydrateAsync(ct);
        ImmutableArray<string> codes = oldCode is null ? [code] : [oldCode, code];
        await BadgeDirectory.RefreshCodesAsync(codes, ct);
        await SendBadgeInventoryAsync(ct);
        await Presence.OnSelectedBadgesChangedAsync(await GetSelectedBadgesAsync(ct), ct);
        await RefreshBadgesRankAsync(ct);
    }
}
