using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Achievements;

namespace Turbo.Inventory.Grains.Badges;

internal sealed partial class PlayerBadgeGrain
{
    /// <summary>
    /// The badge of the level a player is at replaces every lower level of the same achievement,
    /// worn or not, manual or earned: a worn level keeps its slot when the next level arrives.
    /// A higher level the player already owns is kept instead of the granted one.
    /// </summary>
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
        var owned = await db
            .PlayerBadges.Where(x => x.PlayerEntityId == _state.PlayerId.Value)
            .ToListAsync(ct);
        var newBadge = owned.SingleOrDefault(x => x.BadgeCode == code);
        var familyOwned = AchievementBadgeCodes.TryGetBase(code, out var badgeBase)
            ? owned.Where(x => AchievementBadgeCodes.IsLevelOf(badgeBase, x.BadgeCode)).ToList()
            : [];
        // A higher level already owned is the one that stays; the granted level is not added.
        var higher = familyOwned
            .Where(x => AchievementBadgeCodes.LevelOf(x.BadgeCode) > level)
            .MaxBy(x => AchievementBadgeCodes.LevelOf(x.BadgeCode));
        PlayerBadgeEntity kept;
        if (higher is not null)
            kept = higher;
        else
        {
            if (newBadge is null)
            {
                newBadge = new PlayerBadgeEntity
                {
                    PlayerEntityId = _state.PlayerId.Value,
                    BadgeCode = code,
                    SlotId = null,
                    PlayerEntity = null!,
                };
                db.PlayerBadges.Add(newBadge);
            }
            kept = newBadge;
        }
        // Only the kept level stays; every other owned level of the family goes. The family is
        // the badge prefix, which a validated catalog keeps stable and unique per achievement,
        // so no separate record of what each achievement granted is needed.
        var removed = familyOwned.Where(x => x != kept).ToList();
        TransferSlot(kept, removed);
        db.PlayerBadges.RemoveRange(removed);
        await db.SaveChangesAsync(ct);
        await RefreshAfterAchievementChangeAsync([code, .. removed.Select(x => x.BadgeCode)], ct);
    }

    /// <summary>
    /// Collapses each achievement family to the single highest level the player owns, for
    /// badges that were granted before levels replaced one another. Idempotent and quiet when
    /// nothing is owned twice.
    /// </summary>
    public async Task NormalizeAchievementBadgesAsync(
        ImmutableArray<string> badgeBases,
        CancellationToken ct
    )
    {
        if (badgeBases.IsDefaultOrEmpty)
            return;
        await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);
        var owned = await db
            .PlayerBadges.Where(x =>
                x.PlayerEntityId == _state.PlayerId.Value && x.BadgeCode.StartsWith("ACH_")
            )
            .ToListAsync(ct);
        var affected = new List<string>();
        foreach (var badgeBase in badgeBases.Distinct())
        {
            var members = owned
                .Where(x => AchievementBadgeCodes.IsLevelOf(badgeBase, x.BadgeCode))
                .ToList();
            if (members.Count < 2)
                continue;
            var kept = members.MaxBy(x => AchievementBadgeCodes.LevelOf(x.BadgeCode))!;
            var removed = members.Where(x => x != kept).ToList();
            TransferSlot(kept, removed);
            db.PlayerBadges.RemoveRange(removed);
            affected.AddRange(members.Select(x => x.BadgeCode));
        }
        if (affected.Count == 0)
            return;
        await db.SaveChangesAsync(ct);
        await RefreshAfterAchievementChangeAsync(affected, ct);
    }

    /// <summary>The kept badge takes over the lowest worn slot of the badges it replaces.</summary>
    private static void TransferSlot(PlayerBadgeEntity kept, List<PlayerBadgeEntity> removed)
    {
        if (kept.SlotId is > 0)
            return;
        var slot = removed.Where(x => x.SlotId is > 0).Select(x => x.SlotId).Min();
        if (slot is > 0)
            kept.SlotId = slot;
    }

    private async Task RefreshAfterAchievementChangeAsync(
        IEnumerable<string> changedCodes,
        CancellationToken ct
    )
    {
        // Hydration and directory recount are replayable even if a previous send failed after commit.
        await HydrateAsync(ct);
        await BadgeDirectory.RefreshCodesAsync([.. changedCodes.Distinct()], ct);
        await SendBadgeInventoryAsync(ct);
        await Presence.OnSelectedBadgesChangedAsync(await GetSelectedBadgesAsync(ct), ct);
        await RefreshBadgesRankAsync(ct);
    }
}
