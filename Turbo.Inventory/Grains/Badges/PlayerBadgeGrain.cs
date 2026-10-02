using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Badges;
using Turbo.Database.Entities.Players;
using Turbo.Inventory.Configuration;
using Turbo.Primitives.Badges;
using Turbo.Primitives.Badges.Grains;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Messages.Outgoing.Inventory.Badges;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Inventory.Grains.Badges;

/// <summary>
/// The badges a player owns and which of them they wear. Write-through: the row is written
/// first, memory follows, the presence is told once, so there is nothing to flush on
/// deactivation. A badge's owner count and rarity are the hotel's, so every read fills them in
/// from the badge directory instead of keeping a copy that goes stale.
/// <para>
/// Split out of the inventory so that the room's read of a player's worn badges on entry never
/// waits behind furniture work. What this grain awaits — the badge directory and leaderboard,
/// the presence's tells — never calls it back; the player grain is only told.
/// </para>
/// </summary>
internal sealed partial class PlayerBadgeGrain : Grain, IPlayerBadgeGrain
{
    private const int NOT_WORN = 0;

    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly InventoryConfig _inventoryConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IPlayerBadgeGrain> _logger;

    private readonly PlayerBadgeLiveState _state;

    private IPlayerPresenceGrain Presence => _grainFactory.GetPlayerPresenceGrain(_state.PlayerId);
    private IBadgeDirectoryGrain BadgeDirectory => _grainFactory.GetBadgeDirectoryGrain();
    private IBadgeLeaderboardGrain BadgeLeaderboard => _grainFactory.GetBadgeLeaderboardGrain();

    public PlayerBadgeGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<InventoryConfig> inventoryConfig,
        IGrainFactory grainFactory,
        ILogger<IPlayerBadgeGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _inventoryConfig = inventoryConfig.Value;
        _grainFactory = grainFactory;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to hydrate the badges of player {PlayerId}",
                _state.PlayerId
            );

            throw;
        }
    }

    public Task<ImmutableArray<PlayerBadgeSnapshot>> GetAllBadgeSnapshotsAsync(
        CancellationToken ct
    ) => ToSnapshotsAsync([.. _state.BadgesByCode.Values.OrderBy(x => x.BadgeId)], ct);

    public async Task<PlayerBadgeSnapshot?> GetBadgeSnapshotAsync(
        string badgeCode,
        CancellationToken ct
    )
    {
        if (!_state.BadgesByCode.TryGetValue(badgeCode ?? string.Empty, out var badge))
            return null;

        return (await ToSnapshotsAsync([badge], ct))[0];
    }

    public Task<ImmutableArray<PlayerBadgeSnapshot>> GetSelectedBadgesAsync(CancellationToken ct) =>
        ToSnapshotsAsync(
            [.. _state.BadgesByCode.Values.Where(x => x.SlotId > NOT_WORN).OrderBy(x => x.SlotId)],
            ct
        );

    public Task<bool> HasBadgeAsync(string badgeCode, CancellationToken ct) =>
        Task.FromResult(_state.BadgesByCode.ContainsKey(badgeCode ?? string.Empty));

    public async Task<bool> GiveBadgeAsync(string badgeCode, CancellationToken ct)
    {
        if (!TryNormalizeCode(badgeCode, out var code))
            return false;

        if (_state.BadgesByCode.ContainsKey(code))
        {
            await using var existingDb = await _dbCtxFactory.CreateDbContextAsync(ct);
            await existingDb
                .PlayerBadges.Where(x =>
                    x.PlayerEntityId == _state.PlayerId.Value && x.BadgeCode == code
                )
                .ExecuteUpdateAsync(up => up.SetProperty(x => x.ManualGrant, true), ct);
            return false;
        }

        var entity = new PlayerBadgeEntity
        {
            PlayerEntityId = _state.PlayerId.Value,
            BadgeCode = code,
            SlotId = null,
            PlayerEntity = null!,
        };

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            dbCtx.PlayerBadges.Add(entity);

            await dbCtx.SaveChangesAsync(ct);
        }

        var badge = new OwnedBadge(entity.Id, code, NOT_WORN);

        _state.BadgesByCode[code] = badge;

        var info = await BadgeDirectory.OnBadgeGrantedAsync(code, ct);

        // New until the player opens the badges tab - and said to be new before the badge itself
        // arrives: the client fixes a badge's mark when it makes the badge (Flash
        // `BadgesModel.updateBadge` asks the unseen tracker then), so a badge that came first
        // would be counted on the tab but never marked. Awaited for that order; the unseen grain
        // calls nothing back, and awaits its own send. A failure to mark is logged, not thrown:
        // the badge is granted either way.
        try
        {
            await _grainFactory
                .GetPlayerUnseenItemsGrain(_state.PlayerId)
                .AddAsync(UnseenItemCategory.Badge, [badge.BadgeId], CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to mark a new badge for player {PlayerId}",
                _state.PlayerId
            );
        }

        await _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new BadgeReceivedEventMessageComposer { Badge = ToSnapshot(badge, info) },
            ct
        );

        await RefreshBadgesRankAsync(ct);

        return true;
    }

    public async Task<bool> RemoveBadgeAsync(string badgeCode, CancellationToken ct)
    {
        if (!_state.BadgesByCode.TryGetValue(badgeCode ?? string.Empty, out var badge))
            return false;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            if (
                await dbCtx.AchievementBadgeEntitlements.AnyAsync(
                    x => x.PlayerId == _state.PlayerId.Value && x.BadgeCode == badge.BadgeCode,
                    ct
                )
            )
            {
                await dbCtx
                    .PlayerBadges.Where(x => x.Id == badge.BadgeId)
                    .ExecuteUpdateAsync(up => up.SetProperty(x => x.ManualGrant, false), ct);
                return true;
            }
            await dbCtx
                .PlayerBadges.Where(x =>
                    x.Id == badge.BadgeId && x.PlayerEntityId == _state.PlayerId.Value
                )
                .ExecuteDeleteAsync(ct);
        }

        _state.BadgesByCode.Remove(badge.BadgeCode);

        await BadgeDirectory.OnBadgeRevokedAsync(badge.BadgeCode, ct);

        // The client has no "badge removed" message of its own; it is sent the list again.
        var remaining = await GetAllBadgeSnapshotsAsync(ct);

        await SendBadgeInventoryAsync(remaining, ct);

        // What is still worn is already in that list; asking the directory again would only
        // repeat the call.
        if (badge.SlotId > NOT_WORN)
            await Presence.OnSelectedBadgesChangedAsync(
                [.. remaining.Where(x => x.IsWorn).OrderBy(x => x.SlotId)],
                ct
            );

        await RefreshBadgesRankAsync(ct);

        return true;
    }

    public async Task SendBadgeInventoryAsync(CancellationToken ct) =>
        await SendBadgeInventoryAsync(await GetAllBadgeSnapshotsAsync(ct), ct);

    /// <summary>
    /// The badges tab: the list in fragments, then which are worn. The list does not say which
    /// badges are worn; the client learns that from its own entry of the message that also
    /// describes everyone else's. One batch, so the two cannot arrive apart.
    /// </summary>
    private Task SendBadgeInventoryAsync(
        ImmutableArray<PlayerBadgeSnapshot> badges,
        CancellationToken ct
    )
    {
        var composers = ComposerFragments.Build(
            badges,
            _inventoryConfig.BadgeInventoryFragmentSize,
            (total, current, fragment) =>
                new BadgesEventMessageComposer
                {
                    TotalFragments = total,
                    FragmentNo = current,
                    Badges = fragment,
                }
        );

        composers.Add(
            new HabboUserBadgesMessageComposer
            {
                PlayerId = _state.PlayerId,
                Badges = [.. badges.Where(x => x.IsWorn).OrderBy(x => x.SlotId)],
            }
        );

        return Presence.SendComposerAsync(composers, ct);
    }

    /// <summary>
    /// Wears the given codes, one per slot in the order given. The list comes from a client, so
    /// it is cut to the wearable limit and anything not owned, empty or repeated leaves its slot
    /// empty.
    /// </summary>
    public async Task<ImmutableArray<PlayerBadgeSnapshot>> SetActivatedBadgesAsync(
        ImmutableArray<string> badgeCodes,
        CancellationToken ct
    )
    {
        var slotByCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var slots = Math.Min(badgeCodes.Length, _inventoryConfig.MaxActiveBadges);

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
            return await GetSelectedBadgesAsync(ct);

        var changedIds = changed.Select(x => x.BadgeId).ToList();

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            var entities = await dbCtx
                .PlayerBadges.Where(x =>
                    x.PlayerEntityId == _state.PlayerId.Value && changedIds.Contains(x.Id)
                )
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

        var selected = await GetSelectedBadgesAsync(ct);

        await Presence.OnSelectedBadgesChangedAsync(selected, ct);

        return selected;
    }

    /// <summary>
    /// The rank follows from how many badges there are, so it is worked out here and the player
    /// grain, which carries it in the player's summary, is told. Told, never awaited: the player
    /// grain awaits the presence, which this grain awaits too. The player grain ignores a rank
    /// it already has.
    /// </summary>
    public async Task RefreshBadgesRankAsync(CancellationToken ct)
    {
        var rank = await GetRankAsync(ct);

        _grainFactory
            .GetPlayerGrain(_state.PlayerId)
            .SetBadgesRankAsync(rank, CancellationToken.None)
            .LogAndForget(_logger, "tell player {PlayerId} their badges rank", _state.PlayerId);
    }

    public async Task<PlayerBadgeSummarySnapshot> GetBadgeSummaryAsync(CancellationToken ct)
    {
        var badges = await GetAllBadgeSnapshotsAsync(ct);

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

    private async Task HydrateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await dbCtx
            .PlayerBadges.AsNoTracking()
            .Where(x => x.PlayerEntityId == _state.PlayerId.Value)
            .ToListAsync(ct);

        _state.BadgesByCode.Clear();

        foreach (var entity in entities)
            _state.BadgesByCode[entity.BadgeCode] = new OwnedBadge(
                entity.Id,
                entity.BadgeCode,
                entity.SlotId ?? NOT_WORN
            );
    }

    /// <summary>
    /// A rank is decoration: a leaderboard grain that cannot answer must not fail the grant or
    /// the profile that wanted it, so a failure is logged and reads as no rank.
    /// </summary>
    private async Task<int> GetRankAsync(CancellationToken ct)
    {
        try
        {
            return await BadgeLeaderboard.GetTotalBadgesRankAsync(_state.BadgesByCode.Count, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not read the badges rank of player {PlayerId}",
                _state.PlayerId
            );

            return BadgeRanks.NONE;
        }
    }

    private async Task<ImmutableArray<PlayerBadgeSnapshot>> ToSnapshotsAsync(
        ImmutableArray<OwnedBadge> badges,
        CancellationToken ct
    )
    {
        if (badges.Length == 0)
            return [];

        var infos = await BadgeDirectory.GetInfoAsync([.. badges.Select(x => x.BadgeCode)], ct);

        // The directory answers in the order asked.
        return [.. badges.Select((badge, index) => ToSnapshot(badge, infos[index]))];
    }

    private static PlayerBadgeSnapshot ToSnapshot(OwnedBadge badge, BadgeInfoSnapshot info) =>
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
            _state.PlayerId,
            code.Length
        );

        return false;
    }
}
