using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Badges;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Badges;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Inventory.Grains.Modules;

/// <summary>
/// The badges a player owns and which of them they wear. Same shape as the other sections: the
/// row is written first, the list follows, the presence is told once. What differs is that a
/// badge's owner count and rarity are the hotel's, so every read fills them in from the badge
/// directory instead of keeping a copy that goes stale.
/// </summary>
internal sealed class InventoryBadgeModule(
    InventoryGrain inventoryGrain,
    InventoryLiveState liveState,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger logger
)
{
    private const int NOT_WORN = 0;

    private readonly InventoryGrain _inventoryGrain = inventoryGrain;
    private readonly InventoryLiveState _state = liveState;
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly ILogger _logger = logger;

    private int OwnerId => (int)_inventoryGrain.PlayerId;

    public async Task EnsureReadyAsync(CancellationToken ct)
    {
        if (_state.IsBadgesReady)
            return;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await dbCtx
            .PlayerBadges.AsNoTracking()
            .Where(x => x.PlayerEntityId == OwnerId)
            .ToListAsync(ct);

        _state.BadgesByCode.Clear();

        foreach (var entity in entities)
            _state.BadgesByCode[entity.BadgeCode] = new InventoryBadge(
                entity.Id,
                entity.BadgeCode,
                entity.SlotId ?? NOT_WORN
            );

        _state.IsBadgesReady = true;
    }

    public async Task<ImmutableArray<PlayerBadgeSnapshot>> GetAllAsync(CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        return await ToSnapshotsAsync([.. _state.BadgesByCode.Values.OrderBy(x => x.BadgeId)], ct);
    }

    public async Task<PlayerBadgeSnapshot?> GetAsync(string badgeCode, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        if (!_state.BadgesByCode.TryGetValue(badgeCode ?? string.Empty, out var badge))
            return null;

        return (await ToSnapshotsAsync([badge], ct))[0];
    }

    public async Task<ImmutableArray<PlayerBadgeSnapshot>> GetSelectedAsync(CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        return await ToSnapshotsAsync(
            [.. _state.BadgesByCode.Values.Where(x => x.SlotId > NOT_WORN).OrderBy(x => x.SlotId)],
            ct
        );
    }

    public async Task<bool> HasAsync(string badgeCode, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        return _state.BadgesByCode.ContainsKey(badgeCode ?? string.Empty);
    }

    public async Task<bool> GiveAsync(string badgeCode, CancellationToken ct)
    {
        if (!TryNormalizeCode(badgeCode, out var code))
            return false;

        await EnsureReadyAsync(ct);

        if (_state.BadgesByCode.ContainsKey(code))
            return false;

        var entity = new PlayerBadgeEntity
        {
            PlayerEntityId = OwnerId,
            BadgeCode = code,
            SlotId = null,
            PlayerEntity = null!,
        };

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            dbCtx.PlayerBadges.Add(entity);

            await dbCtx.SaveChangesAsync(ct);
        }

        var badge = new InventoryBadge(entity.Id, code, NOT_WORN);

        _state.BadgesByCode[code] = badge;

        var info = await _inventoryGrain.BadgeDirectory.OnBadgeGrantedAsync(code, ct);

        await _inventoryGrain.Presence.OnBadgeReceivedAsync(ToSnapshot(badge, info), ct);
        await RefreshRankAsync(ct);

        return true;
    }

    public async Task<bool> RemoveAsync(string badgeCode, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        if (!_state.BadgesByCode.TryGetValue(badgeCode ?? string.Empty, out var badge))
            return false;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            await dbCtx
                .PlayerBadges.Where(x => x.Id == badge.BadgeId && x.PlayerEntityId == OwnerId)
                .ExecuteDeleteAsync(ct);
        }

        _state.BadgesByCode.Remove(badge.BadgeCode);

        await _inventoryGrain.BadgeDirectory.OnBadgeRevokedAsync(badge.BadgeCode, ct);

        // The client has no "badge removed" message of its own; it is sent the list again. The
        // list goes with the call: the presence must never have to ask this grain for it, because
        // this grain is waiting on the presence right now.
        var remaining = await GetAllAsync(ct);

        await _inventoryGrain.Presence.SendBadgeInventoryAsync(remaining, ct);

        // What is still worn is already in that list; asking the directory again would only
        // repeat the call.
        if (badge.SlotId > NOT_WORN)
            await _inventoryGrain.Presence.OnSelectedBadgesChangedAsync(
                [.. remaining.Where(x => x.IsWorn).OrderBy(x => x.SlotId)],
                ct
            );

        await RefreshRankAsync(ct);

        return true;
    }

    /// <summary>
    /// Wears the given codes, one per slot in the order given. The list comes from a client, so
    /// it is cut to the wearable limit and anything not owned, empty or repeated leaves its slot
    /// empty.
    /// </summary>
    public async Task<ImmutableArray<PlayerBadgeSnapshot>> SetActivatedAsync(
        ImmutableArray<string> badgeCodes,
        CancellationToken ct
    )
    {
        await EnsureReadyAsync(ct);

        var slotByCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var slots = Math.Min(badgeCodes.Length, _inventoryGrain._inventoryConfig.MaxActiveBadges);

        for (var i = 0; i < slots; i++)
        {
            var code = badgeCodes[i];

            if (
                !string.IsNullOrEmpty(code)
                && _state.BadgesByCode.TryGetValue(code, out var owned)
                && !slotByCode.ContainsKey(owned.BadgeCode)
            )
                slotByCode[owned.BadgeCode] = i + 1;
        }

        var changed = _state
            .BadgesByCode.Values.Where(x => x.SlotId != slotByCode.GetValueOrDefault(x.BadgeCode))
            .ToList();

        if (changed.Count == 0)
            return await GetSelectedAsync(ct);

        var changedIds = changed.Select(x => x.BadgeId).ToList();

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            var entities = await dbCtx
                .PlayerBadges.Where(x => x.PlayerEntityId == OwnerId && changedIds.Contains(x.Id))
                .ToListAsync(ct);

            foreach (var entity in entities)
                entity.SlotId = slotByCode.TryGetValue(entity.BadgeCode, out var slot)
                    ? slot
                    : null;

            await dbCtx.SaveChangesAsync(ct);
        }

        foreach (var badge in changed)
            _state.BadgesByCode[badge.BadgeCode] = badge with
            {
                SlotId = slotByCode.GetValueOrDefault(badge.BadgeCode),
            };

        var selected = await GetSelectedAsync(ct);

        await _inventoryGrain.Presence.OnSelectedBadgesChangedAsync(selected, ct);

        return selected;
    }

    /// <summary>
    /// The rank follows from how many badges there are, so it is worked out here and the player
    /// grain, which carries it in the player's summary, is told. Told, never awaited: nothing the
    /// inventory awaits may lead back to it, and the player grain awaits the presence, which the
    /// inventory awaits too. The player grain ignores a rank it already has.
    /// </summary>
    public async Task RefreshRankAsync(CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        var rank = await GetRankAsync(ct);

        _inventoryGrain
            .Player.SetBadgesRankAsync(rank, CancellationToken.None)
            .LogAndForget(_logger, $"tell player {_inventoryGrain.PlayerId} their badges rank");
    }

    public async Task<PlayerBadgeSummarySnapshot> GetSummaryAsync(CancellationToken ct)
    {
        var badges = await GetAllAsync(ct);

        return new PlayerBadgeSummarySnapshot
        {
            TotalBadges = badges.Length,
            TotalBadgesRank = await GetRankAsync(ct),
            RarityCounts =
            [
                .. badges
                    .GroupBy(x => x.Rarity)
                    .OrderBy(x => x.Key)
                    .Select(x => new BadgeRarityCountSnapshot
                    {
                        RarityId = (byte)x.Key,
                        Count = x.Count(),
                    }),
            ],
        };
    }

    /// <summary>
    /// A rank is decoration: a leaderboard grain that cannot answer must not fail the grant or
    /// the profile that wanted it, so a failure is logged and reads as no rank.
    /// </summary>
    private async Task<int> GetRankAsync(CancellationToken ct)
    {
        try
        {
            return await _inventoryGrain.BadgeLeaderboard.GetTotalBadgesRankAsync(
                _state.BadgesByCode.Count,
                ct
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not read the badges rank of player {PlayerId}",
                _inventoryGrain.PlayerId
            );

            return BadgeRanks.NONE;
        }
    }

    private async Task<ImmutableArray<PlayerBadgeSnapshot>> ToSnapshotsAsync(
        ImmutableArray<InventoryBadge> badges,
        CancellationToken ct
    )
    {
        if (badges.Length == 0)
            return [];

        var infos = await _inventoryGrain.BadgeDirectory.GetInfoAsync(
            [.. badges.Select(x => x.BadgeCode)],
            ct
        );

        // The directory answers in the order asked.
        return [.. badges.Select((badge, index) => ToSnapshot(badge, infos[index]))];
    }

    private static PlayerBadgeSnapshot ToSnapshot(InventoryBadge badge, BadgeInfoSnapshot info) =>
        new()
        {
            BadgeId = badge.BadgeId,
            BadgeCode = badge.BadgeCode,
            SlotId = badge.SlotId,
            OwnerCount = info.OwnerCount,
            Rarity = info.Rarity,
        };

    /// <summary>A badge code is a short plain token; anything else is refused and logged.</summary>
    private bool TryNormalizeCode(string? badgeCode, out string code)
    {
        code = badgeCode?.Trim() ?? string.Empty;

        if (
            code.Length > 0
            && code.Length <= BadgeDefinitionEntity.BADGE_CODE_MAX_LENGTH
            && code.All(c => char.IsLetterOrDigit(c) || c is '_' or '-')
        )
            return true;

        _logger.LogWarning(
            "Refused to give player {PlayerId} a badge with a malformed code of length {Length}",
            _inventoryGrain.PlayerId,
            code.Length
        );

        return false;
    }
}
