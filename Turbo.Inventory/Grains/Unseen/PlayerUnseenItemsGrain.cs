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
using Turbo.Database.Entities.Players;
using Turbo.Inventory.Configuration;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Inventory.Grains.Unseen;

/// <summary>
/// What a player received and has not looked at yet, per inventory tab. Write-through: a new
/// id is written, then kept, then the client is told; nothing to flush on deactivation.
/// <para>
/// Ids go stale without anyone saying so — an item is placed, traded, sold or deleted, and the
/// client drops a single new item on its own without telling the server. So the rows are pruned
/// against what the player still has whenever the grain loads, and each tab is capped
/// (<c>InventoryConfig.MaxUnseenItemsPerCategory</c>).
/// </para>
/// <para>
/// It reads the inventory's tables but never writes them, and calls nothing but the player's
/// presence (a tell), so the inventory and badge grains may tell it anything.
/// </para>
/// </summary>
internal sealed class PlayerUnseenItemsGrain : Grain, IPlayerUnseenItemsGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly InventoryConfig _inventoryConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IPlayerUnseenItemsGrain> _logger;

    private readonly PlayerUnseenItemsLiveState _state;

    public PlayerUnseenItemsGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<InventoryConfig> inventoryConfig,
        IGrainFactory grainFactory,
        ILogger<IPlayerUnseenItemsGrain> logger
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
                "Failed to hydrate the unseen items of player {PlayerId}",
                _state.PlayerId
            );

            throw;
        }
    }

    public Task<ImmutableDictionary<UnseenItemCategory, ImmutableArray<int>>> GetUnseenItemsAsync(
        CancellationToken ct
    ) => Task.FromResult(Snapshot(_state.IdsByCategory));

    public async Task AddAsync(
        UnseenItemCategory category,
        ImmutableArray<int> ids,
        CancellationToken ct
    )
    {
        var known = GetIds(category);
        var added = ids.Where(id => id > 0 && !known.Contains(id)).Distinct().ToList();

        if (added.Count == 0)
            return;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            dbCtx.AddRange(
                added.Select(id => new PlayerUnseenItemEntity
                {
                    PlayerEntityId = _state.PlayerId.Value,
                    Category = category,
                    ItemId = id,
                })
            );

            await dbCtx.SaveChangesAsync(ct);
        }

        foreach (var id in added)
            known.Add(id);

        await TrimAsync(category, ct);

        // Only what is new goes out; the client adds it to what it already marks.
        await _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new UnseenItemsEventMessageComposer
            {
                Items = ImmutableDictionary<UnseenItemCategory, ImmutableArray<int>>.Empty.Add(
                    category,
                    [.. added.Where(known.Contains)]
                ),
            },
            ct
        );
    }

    public async Task ResetCategoryAsync(UnseenItemCategory category, CancellationToken ct)
    {
        if (!_state.IdsByCategory.TryGetValue(category, out var known) || known.Count == 0)
            return;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            await dbCtx
                .PlayerUnseenItems.Where(x =>
                    x.PlayerEntityId == _state.PlayerId.Value && x.Category == category
                )
                .ExecuteDeleteAsync(ct);
        }

        known.Clear();
    }

    public async Task ResetItemsAsync(
        UnseenItemCategory category,
        ImmutableArray<int> ids,
        CancellationToken ct
    )
    {
        if (!_state.IdsByCategory.TryGetValue(category, out var known))
            return;

        var seen = ids.Where(known.Contains).Distinct().ToList();

        if (seen.Count == 0)
            return;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            await dbCtx
                .PlayerUnseenItems.Where(x =>
                    x.PlayerEntityId == _state.PlayerId.Value
                    && x.Category == category
                    && seen.Contains(x.ItemId)
                )
                .ExecuteDeleteAsync(ct);
        }

        foreach (var id in seen)
            known.Remove(id);
    }

    /// <summary>
    /// Loads the rows and drops the ones whose item the player no longer holds in the inventory:
    /// placed, traded, sold or deleted since it arrived.
    /// </summary>
    private async Task HydrateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var rows = await dbCtx
            .PlayerUnseenItems.AsNoTracking()
            .Where(x => x.PlayerEntityId == _state.PlayerId.Value)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Category,
                x.ItemId,
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
            return;

        var held = await LoadHeldIdsAsync(
            dbCtx,
            rows.Select(x => (x.Category, x.ItemId)).ToList(),
            ct
        );

        var stale = new List<int>();

        foreach (var row in rows)
        {
            if (held.TryGetValue(row.Category, out var ids) && ids.Contains(row.ItemId))
                GetIds(row.Category).Add(row.ItemId);
            else
                stale.Add(row.Id);
        }

        if (stale.Count > 0)
            await dbCtx.PlayerUnseenItems.Where(x => stale.Contains(x.Id)).ExecuteDeleteAsync(ct);
    }

    /// <summary>Of the given ids, the ones still in the player's inventory, per category.</summary>
    private async Task<Dictionary<UnseenItemCategory, HashSet<int>>> LoadHeldIdsAsync(
        TurboDbContext dbCtx,
        List<(UnseenItemCategory Category, int ItemId)> wanted,
        CancellationToken ct
    )
    {
        List<int> Of(params UnseenItemCategory[] categories) =>
            [.. wanted.Where(x => categories.Contains(x.Category)).Select(x => x.ItemId)];

        var furniIds = Of(UnseenItemCategory.OwnedFurni, UnseenItemCategory.RentedFurni);
        var petIds = Of(UnseenItemCategory.Pet);
        var botIds = Of(UnseenItemCategory.Bot);
        var badgeIds = Of(UnseenItemCategory.Badge);

        var heldFurni =
            furniIds.Count == 0
                ? []
                : await dbCtx
                    .Furnitures.Where(x =>
                        x.PlayerEntityId == _state.PlayerId.Value
                        && x.RoomEntityId == null
                        && x.ChestItemEntityId == null
                        && furniIds.Contains(x.Id)
                    )
                    .Select(x => x.Id)
                    .ToListAsync(ct);
        var heldPets =
            petIds.Count == 0
                ? []
                : await dbCtx
                    .Pets.Where(x =>
                        x.PlayerEntityId == _state.PlayerId.Value
                        && x.RoomEntityId == null
                        && petIds.Contains(x.Id)
                    )
                    .Select(x => x.Id)
                    .ToListAsync(ct);
        var heldBots =
            botIds.Count == 0
                ? []
                : await dbCtx
                    .Bots.Where(x =>
                        x.PlayerEntityId == _state.PlayerId.Value
                        && x.RoomEntityId == null
                        && botIds.Contains(x.Id)
                    )
                    .Select(x => x.Id)
                    .ToListAsync(ct);
        var heldBadges =
            badgeIds.Count == 0
                ? []
                : await dbCtx
                    .PlayerBadges.Where(x =>
                        x.PlayerEntityId == _state.PlayerId.Value && badgeIds.Contains(x.Id)
                    )
                    .Select(x => x.Id)
                    .ToListAsync(ct);

        return new()
        {
            [UnseenItemCategory.OwnedFurni] = [.. heldFurni],
            [UnseenItemCategory.RentedFurni] = [.. heldFurni],
            [UnseenItemCategory.Pet] = [.. heldPets],
            [UnseenItemCategory.Bot] = [.. heldBots],
            [UnseenItemCategory.Badge] = [.. heldBadges],
        };
    }

    /// <summary>
    /// Keeps a tab to the configured size: past it, the oldest ids stop being new. Ids are
    /// database ids, so the lowest are the oldest.
    /// </summary>
    private async Task TrimAsync(UnseenItemCategory category, CancellationToken ct)
    {
        var known = GetIds(category);
        var excess = known.Count - Math.Max(1, _inventoryConfig.MaxUnseenItemsPerCategory);

        if (excess <= 0)
            return;

        var dropped = known.Order().Take(excess).ToList();

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            await dbCtx
                .PlayerUnseenItems.Where(x =>
                    x.PlayerEntityId == _state.PlayerId.Value
                    && x.Category == category
                    && dropped.Contains(x.ItemId)
                )
                .ExecuteDeleteAsync(ct);
        }

        foreach (var id in dropped)
            known.Remove(id);
    }

    private HashSet<int> GetIds(UnseenItemCategory category)
    {
        if (!_state.IdsByCategory.TryGetValue(category, out var ids))
        {
            ids = [];
            _state.IdsByCategory[category] = ids;
        }

        return ids;
    }

    private static ImmutableDictionary<UnseenItemCategory, ImmutableArray<int>> Snapshot(
        Dictionary<UnseenItemCategory, HashSet<int>> idsByCategory
    ) =>
        idsByCategory
            .Where(x => x.Value.Count > 0)
            .ToImmutableDictionary(x => x.Key, x => x.Value.Order().ToImmutableArray());
}
