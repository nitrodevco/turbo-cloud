using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Orleans;

namespace Turbo.Inventory.Grains.Badges;

internal sealed partial class PlayerBadgeGrain
{
    /// <summary>
    /// The badge of the level a player is at replaces every lower level of the same achievement,
    /// worn or not, manual or earned: a worn level keeps its slot when the next level arrives.
    /// A higher level the player already owns is kept instead of the granted one. The
    /// achievement's levels are its own list of codes, so a code that ends in a digit
    /// (<c>ACH_Profile1</c>) is never read as a level of another (<c>ACH_Profile2</c>).
    /// </summary>
    public async Task GrantAchievementAsync(
        int achievementId,
        int level,
        ImmutableArray<string> levelCodes,
        CancellationToken ct
    )
    {
        if (
            achievementId <= 0
            || levelCodes.IsDefault
            || level <= 0
            || level > levelCodes.Length
            || !TryNormalizeCode(levelCodes[level - 1], out var code)
        )
            throw new ArgumentException("Invalid achievement badge entitlement.");
        await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);
        var owned = await db
            .PlayerBadges.Where(x => x.PlayerEntityId == _state.PlayerId.Value)
            .ToListAsync(ct);
        var newBadge = owned.SingleOrDefault(x => x.BadgeCode == code);
        var familyOwned = owned.Where(x => LevelIn(levelCodes, x.BadgeCode) > 0).ToList();
        // A higher level already owned is the one that stays; the granted level is not added.
        var higher = familyOwned
            .Where(x => LevelIn(levelCodes, x.BadgeCode) > level)
            .MaxBy(x => LevelIn(levelCodes, x.BadgeCode));
        PlayerBadgeEntity kept;
        var added = false;
        if (higher is not null)
            kept = higher;
        else
        {
            if (newBadge is null)
            {
                added = true;
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
        // Only the kept level stays; every other owned level of the family goes. A validated
        // catalog keeps every code unique to one achievement's level, so no separate record of
        // what each achievement granted is needed.
        var removed = familyOwned.Where(x => x != kept).ToList();
        TransferSlot(kept, removed);
        db.PlayerBadges.RemoveRange(removed);
        await db.SaveChangesAsync(ct);

        if (higher is not null)
        {
            // The client is told of the granted level by the achievement notification and adds
            // it; the list, sent again, keeps the level the player really owns.
            await RefreshAfterAchievementChangeAsync(
                [code, .. removed.Select(x => x.BadgeCode)],
                ct
            );
            return;
        }

        await HydrateAsync(ct);
        await BadgeDirectory.RefreshCodesAsync([code, .. removed.Select(x => x.BadgeCode)], ct);

        // Habbo marks the new level's badge unseen before the achievement notification that adds
        // it to the client's list (every recorded level-up: UnseenItems {4: [badge id]}, then
        // HabboAchievementNotification), and sends no badge list: the notification carries the
        // new badge and the level it replaces. Awaited for that order; a failure to mark is
        // logged, the level is granted either way.
        if (added)
        {
            try
            {
                await _grainFactory
                    .GetPlayerUnseenItemsGrain(_state.PlayerId)
                    .AddAsync(UnseenItemCategory.Badge, [kept.Id], CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to mark achievement badge {BadgeCode} new for player {PlayerId}",
                    code,
                    _state.PlayerId
                );
            }
        }

        // A worn level hands its slot on (TransferSlot); the client and the room are told after the
        // notification (PlayerAchievementGrain.PublishAsync), which takes the badge off in the
        // client's list.
        await RefreshBadgesRankAsync(ct);
    }

    /// <summary>
    /// Collapses each achievement family (one achievement's level codes, in level order) to the
    /// single highest level the player owns, for badges that were granted before levels replaced
    /// one another. Idempotent and quiet when nothing is owned twice.
    /// </summary>
    public async Task NormalizeAchievementBadgesAsync(
        ImmutableArray<ImmutableArray<string>> families,
        CancellationToken ct
    )
    {
        if (families.IsDefaultOrEmpty)
            return;
        await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);
        var owned = await db
            .PlayerBadges.Where(x =>
                x.PlayerEntityId == _state.PlayerId.Value && x.BadgeCode.StartsWith("ACH_")
            )
            .ToListAsync(ct);
        var affected = new List<string>();
        foreach (var levelCodes in families.Where(x => !x.IsDefaultOrEmpty))
        {
            var members = owned.Where(x => LevelIn(levelCodes, x.BadgeCode) > 0).ToList();
            if (members.Count < 2)
                continue;
            var kept = members.MaxBy(x => LevelIn(levelCodes, x.BadgeCode))!;
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

    /// <summary>The level (1-based) a code is in the achievement's list; zero when not one of them.</summary>
    private static int LevelIn(ImmutableArray<string> levelCodes, string code) =>
        levelCodes.IndexOf(code, StringComparer.Ordinal) + 1;

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
