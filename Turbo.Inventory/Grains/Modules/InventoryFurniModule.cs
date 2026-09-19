using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Inventory.Grains.Modules;

/// <summary>
/// The furniture a player keeps in the inventory (rows in no room). Same rules as the pet and
/// bot modules: the section loads on first use, the row is written before the list changes,
/// and the presence is told once per change, not once per item.
/// </summary>
internal sealed class InventoryFurniModule(
    InventoryGrain inventoryGrain,
    InventoryLiveState liveState,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IInventoryFurnitureLoader furnitureLoader,
    IFurnitureDefinitionProvider definitionProvider,
    ICatalogService catalogService,
    ILogger logger
)
{
    private readonly InventoryGrain _inventoryGrain = inventoryGrain;
    private readonly InventoryLiveState _state = liveState;
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly IInventoryFurnitureLoader _furnitureLoader = furnitureLoader;
    private readonly IFurnitureDefinitionProvider _definitionProvider = definitionProvider;
    private readonly ICatalogService _catalogService = catalogService;
    private readonly ILogger _logger = logger;

    public async Task EnsureReadyAsync(CancellationToken ct)
    {
        if (_state.IsFurnitureReady)
            return;

        var items = await _furnitureLoader.LoadByPlayerIdAsync(
            _inventoryGrain.PlayerId,
            await _inventoryGrain.GetOwnerNameAsync(ct),
            ct
        );

        _state.FurnitureById.Clear();

        foreach (var item in items)
            _state.FurnitureById[item.ItemId] = item;

        _state.IsFurnitureReady = true;
    }

    public async Task<ImmutableArray<FurnitureItemSnapshot>> GetAllAsync(CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        return [.. _state.FurnitureById.Values.Select(x => x.GetSnapshot())];
    }

    public async Task<FurnitureItemSnapshot?> GetAsync(RoomObjectId itemId, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        return _state.FurnitureById.TryGetValue(itemId, out var item) ? item.GetSnapshot() : null;
    }

    /// <summary>The items of these ids held here; ids not held are left out.</summary>
    public async Task<ImmutableArray<FurnitureItemSnapshot>> GetManyAsync(
        ImmutableArray<RoomObjectId> itemIds,
        CancellationToken ct
    )
    {
        await EnsureReadyAsync(ct);

        var found = ImmutableArray.CreateBuilder<FurnitureItemSnapshot>(itemIds.Length);

        foreach (var itemId in itemIds)
        {
            if (_state.FurnitureById.TryGetValue(itemId, out var item))
                found.Add(item.GetSnapshot());
        }

        return found.ToImmutable();
    }

    /// <summary>
    /// Items picked up from a room; the room has already released the rows. Items owned by
    /// someone else are refused. Returns how many were listed.
    /// </summary>
    public async Task<int> AddFromRoomItemsAsync(
        ImmutableArray<RoomItemSnapshot> snapshots,
        CancellationToken ct
    )
    {
        if (snapshots.IsDefaultOrEmpty)
            return 0;

        await EnsureReadyAsync(ct);

        var items = new List<IFurnitureItem>(snapshots.Length);

        foreach (var snapshot in snapshots)
        {
            if (snapshot.OwnerId != _inventoryGrain.PlayerId)
            {
                _logger.LogWarning(
                    "Item {ItemId} of player {OwnerId} was handed to the inventory of player {PlayerId}; refused",
                    snapshot.ObjectId,
                    snapshot.OwnerId,
                    _inventoryGrain.PlayerId
                );

                continue;
            }

            items.Add(_furnitureLoader.CreateFromRoomItemSnapshot(snapshot));
        }

        return await AddAsync(items, ct);
    }

    public async Task<bool> RemoveAsync(RoomObjectId itemId, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        if (!_state.FurnitureById.Remove(itemId))
            return false;

        await _inventoryGrain.Presence.OnFurnitureRemovedAsync([itemId], ct);

        return true;
    }

    /// <summary>
    /// Creates one row per definition and lists the items. One insert and one presence call,
    /// however many items a purchase grants.
    /// </summary>
    public async Task<ImmutableArray<FurnitureItemSnapshot>> GrantAsync(
        IReadOnlyList<(FurnitureDefinitionSnapshot Definition, string? ExtraDataJson)> grants,
        CancellationToken ct
    )
    {
        if (grants.Count == 0)
            return [];

        await EnsureReadyAsync(ct);

        var entities = grants
            .Select(grant => new FurnitureEntity
            {
                PlayerEntityId = (int)_inventoryGrain.PlayerId,
                FurnitureDefinitionEntityId = grant.Definition.Id,
                ExtraData = grant.ExtraDataJson,
            })
            .ToList();

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            dbCtx.AddRange(entities);

            await dbCtx.SaveChangesAsync(ct);
        }

        var ownerName = await _inventoryGrain.GetOwnerNameAsync(ct);
        var items = new List<IFurnitureItem>(entities.Count);

        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];

            items.Add(
                _furnitureLoader.Create(
                    entity.Id,
                    _inventoryGrain.PlayerId,
                    ownerName,
                    grants[i].Definition,
                    entity.ExtraData,
                    entity.CreatedAt
                )
            );
        }

        await AddAsync(items, ct);

        return [.. items.Select(x => x.GetSnapshot())];
    }

    /// <summary>One item of a definition (a saddle taken off a horse, a harvested seed).</summary>
    public async Task<FurnitureItemSnapshot?> GrantAsync(
        int definitionId,
        string? extraDataJson,
        CancellationToken ct
    )
    {
        var definition = _definitionProvider.TryGetDefinition(definitionId);

        if (definition is null)
        {
            _logger.LogError(
                "Furniture definition {DefinitionId} is missing; cannot grant it to player {PlayerId}",
                definitionId,
                _inventoryGrain.PlayerId
            );

            return null;
        }

        var granted = await GrantAsync([(definition, extraDataJson)], ct);

        return granted.Length > 0 ? granted[0] : null;
    }

    /// <summary>A limited edition item: the serial travels in the stuff section of its extra data.</summary>
    public async Task GrantLimitedAsync(
        int catalogProductId,
        int serialNumber,
        int seriesSize,
        CancellationToken ct
    )
    {
        var catalog = _catalogService.GetCatalogSnapshot(CatalogType.Normal);

        if (!catalog.ProductsById.TryGetValue(catalogProductId, out var product))
        {
            _logger.LogError(
                "Catalog product {CatalogProductId} is missing; cannot grant the limited item to player {PlayerId}",
                catalogProductId,
                _inventoryGrain.PlayerId
            );

            throw new TurboException(TurboErrorCodeEnum.CatalogProductNotFound);
        }

        var extraDataJson = JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                [ExtraDataSectionType.STUFF] = new
                {
                    UniqueNumber = serialNumber,
                    UniqueSeries = seriesSize,
                    Data = "0",
                },
            }
        );

        await GrantAsync([(GetDefinitionOrThrow(product.FurniDefinitionId), extraDataJson)], ct);
    }

    /// <summary>
    /// Moves items held here to another player (a completed trade). All or nothing: the rows
    /// change owner in one guarded statement, then both lists and both clients follow.
    /// </summary>
    public async Task<bool> TransferAsync(
        ImmutableArray<RoomObjectId> itemIds,
        PlayerId toPlayerId,
        CancellationToken ct
    )
    {
        if (itemIds.IsDefaultOrEmpty)
            return true;

        await EnsureReadyAsync(ct);

        var items = new List<IFurnitureItem>(itemIds.Length);

        foreach (var itemId in itemIds)
        {
            if (!_state.FurnitureById.TryGetValue(itemId, out var item))
            {
                _logger.LogWarning(
                    "Player {PlayerId} no longer holds item {ItemId}; the transfer to {ToPlayerId} is refused",
                    _inventoryGrain.PlayerId,
                    itemId,
                    toPlayerId
                );

                return false;
            }

            items.Add(item);
        }

        var ids = items.Select(x => x.ItemId.Value).ToList();

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        await using (var tx = await dbCtx.Database.BeginTransactionAsync(ct))
        {
            var updated = await dbCtx
                .Furnitures.Where(x =>
                    ids.Contains(x.Id)
                    && x.PlayerEntityId == (int)_inventoryGrain.PlayerId
                    && x.RoomEntityId == null
                )
                .ExecuteUpdateAsync(
                    up => up.SetProperty(x => x.PlayerEntityId, toPlayerId.Value),
                    ct
                );

            if (updated != ids.Count)
            {
                await tx.RollbackAsync(ct);

                _logger.LogError(
                    "Transfer of {Count} items from player {PlayerId} to {ToPlayerId} changed {Updated} rows; rolled back",
                    ids.Count,
                    _inventoryGrain.PlayerId,
                    toPlayerId,
                    updated
                );

                // The list and the rows disagree; reload rather than keep trading on it.
                _state.IsFurnitureReady = false;

                return false;
            }

            await tx.CommitAsync(ct);
        }

        foreach (var item in items)
            _state.FurnitureById.Remove(item.ItemId);

        await _inventoryGrain.Presence.OnFurnitureRemovedAsync(
            [.. items.Select(x => x.ItemId)],
            ct
        );
        await _inventoryGrain
            .GetInventoryOf(toPlayerId)
            .ReceiveFurnitureAsync([.. items.Select(x => x.GetSnapshot())], ct);

        return true;
    }

    /// <summary>Items whose rows were just re-owned to this player by another inventory.</summary>
    public async Task ReceiveAsync(
        ImmutableArray<FurnitureItemSnapshot> snapshots,
        CancellationToken ct
    )
    {
        if (snapshots.IsDefaultOrEmpty)
            return;

        await EnsureReadyAsync(ct);

        var ownerName = await _inventoryGrain.GetOwnerNameAsync(ct);

        await AddAsync(
            [
                .. snapshots.Select(snapshot =>
                    _furnitureLoader.CreateFromFurnitureItemSnapshot(
                        snapshot,
                        _inventoryGrain.PlayerId,
                        ownerName
                    )
                ),
            ],
            ct
        );
    }

    /// <summary>
    /// The definition a grant needs. A missing one means the catalog and the furniture data no
    /// longer agree, so it is logged with the id before the purchase is failed.
    /// </summary>
    public FurnitureDefinitionSnapshot GetDefinitionOrThrow(int definitionId)
    {
        var definition = _definitionProvider.TryGetDefinition(definitionId);

        if (definition is not null)
            return definition;

        _logger.LogError(
            "Furniture definition {DefinitionId} is missing; cannot grant it to player {PlayerId}",
            definitionId,
            _inventoryGrain.PlayerId
        );

        throw new TurboException(TurboErrorCodeEnum.FurnitureDefinitionNotFound);
    }

    /// <summary>Lists items and tells the presence once. Returns how many were new.</summary>
    private async Task<int> AddAsync(IReadOnlyList<IFurnitureItem> items, CancellationToken ct)
    {
        var added = ImmutableArray.CreateBuilder<FurnitureItemSnapshot>(items.Count);

        foreach (var item in items)
        {
            // A load that ran after the row was released may already have listed it.
            if (_state.FurnitureById.TryAdd(item.ItemId, item))
                added.Add(item.GetSnapshot());
        }

        if (added.Count > 0)
            await _inventoryGrain.Presence.OnFurnitureAddedAsync(added.ToImmutable(), ct);

        return added.Count;
    }
}
