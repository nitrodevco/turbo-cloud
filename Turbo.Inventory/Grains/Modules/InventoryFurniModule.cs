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
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Orleans;
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
) : InventoryGrainComponent(inventoryGrain)
{
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

        var receivesBefore = _state.FurnitureReceiveCount;
        var items = await _furnitureLoader.LoadByPlayerIdAsync(
            PlayerId,
            await GetOwnerNameAsync(ct),
            ct
        );

        _state.FurnitureById.Clear();

        foreach (var item in items)
            _state.FurnitureById[item.ItemId] = item;

        // A receive landed while the rows were read, and they may not include it: serve this
        // call from what was read and load again next time.
        _state.IsFurnitureReady = receivesBefore == _state.FurnitureReceiveCount;
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
            if (snapshot.OwnerId != PlayerId)
            {
                _logger.LogWarning(
                    "Item {ItemId} of player {OwnerId} was handed to the inventory of player {PlayerId}; refused",
                    snapshot.ObjectId,
                    snapshot.OwnerId,
                    PlayerId
                );

                continue;
            }

            items.Add(_furnitureLoader.CreateFromRoomItemSnapshot(snapshot));
        }

        return await AddAsync(items, ct);
    }

    public async Task ReleaseAsync(ImmutableArray<RoomObjectId> itemIds, CancellationToken ct)
    {
        if (itemIds.IsDefaultOrEmpty)
            return;

        await EnsureReadyAsync(ct);

        ImmutableArray<RoomObjectId> released =
        [
            .. itemIds.Where(itemId => _state.FurnitureById.Remove(itemId)),
        ];

        if (released.IsEmpty)
            return;

        await SendRemovedAsync(released, ct);
    }

    public async Task<bool> RemoveAsync(RoomObjectId itemId, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        if (!_state.FurnitureById.Remove(itemId))
            return false;

        await SendRemovedAsync([itemId], ct);

        return true;
    }

    /// <summary>
    /// Uses an item up (a room paper applied to a room): its row is deleted, then it leaves the
    /// list. Only an item held here, in no room and no chest, is deleted; null otherwise.
    /// </summary>
    public async Task<FurnitureItemSnapshot?> ConsumeAsync(
        RoomObjectId itemId,
        CancellationToken ct
    )
    {
        await EnsureReadyAsync(ct);

        if (!_state.FurnitureById.TryGetValue(itemId, out var item))
            return null;

        int deleted;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            deleted = await dbCtx
                .Furnitures.Where(x =>
                    x.Id == (int)itemId
                    && x.PlayerEntityId == (int)PlayerId
                    && x.RoomEntityId == null
                    && x.ChestItemEntityId == null
                )
                .ExecuteDeleteAsync(ct);
        }

        if (deleted == 0)
        {
            _logger.LogWarning(
                "Furniture {ItemId} is listed in the inventory of player {PlayerId} but has no row there; not used up",
                itemId,
                PlayerId
            );

            return null;
        }

        _state.FurnitureById.Remove(itemId);

        await SendRemovedAsync([itemId], ct);

        return item.GetSnapshot();
    }

    /// <summary>
    /// Creates one row per definition and lists the items. One insert and one presence call,
    /// however many items a purchase grants. A section that is not loaded is not loaded for
    /// this: the rows are written, the client is told its list changed, and the next read
    /// loads them with everything else.
    /// </summary>
    public async Task<ImmutableArray<FurnitureItemSnapshot>> GrantAsync(
        IReadOnlyList<(FurnitureDefinitionSnapshot Definition, string? ExtraDataJson)> grants,
        CancellationToken ct
    )
    {
        if (grants.Count == 0)
            return [];

        var entities = grants
            .Select(grant => new FurnitureEntity
            {
                PlayerEntityId = (int)PlayerId,
                FurnitureDefinitionEntityId = grant.Definition.Id,
                ExtraData = grant.ExtraDataJson,
            })
            .ToList();

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            dbCtx.AddRange(entities);

            await dbCtx.SaveChangesAsync(ct);
        }

        return await ListGrantedAsync(
            entities,
            grants.Select(grant => grant.Definition).ToList(),
            ct
        );
    }

    /// <summary>
    /// Teleporters, each unit a linked pair: two rows, each naming the other in its
    /// <c>room_linker</c> section. The ids exist only once the rows are inserted, so the pair is
    /// inserted, linked and committed in one transaction — no half is ever left unlinked.
    /// </summary>
    public async Task<ImmutableArray<FurnitureItemSnapshot>> GrantTeleportPairsAsync(
        IReadOnlyList<FurnitureDefinitionSnapshot> definitions,
        CancellationToken ct
    )
    {
        if (definitions.Count == 0)
            return [];

        var pairDefinitions = definitions
            .SelectMany(definition => new[] { definition, definition })
            .ToList();
        var entities = pairDefinitions
            .Select(definition => new FurnitureEntity
            {
                PlayerEntityId = (int)PlayerId,
                FurnitureDefinitionEntityId = definition.Id,
            })
            .ToList();

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            await using var tx = await dbCtx.Database.BeginTransactionAsync(ct);

            dbCtx.AddRange(entities);

            await dbCtx.SaveChangesAsync(ct);

            for (var i = 0; i < entities.Count; i += 2)
            {
                entities[i].ExtraData = TeleportFurniture.PairExtraData(entities[i + 1].Id);
                entities[i + 1].ExtraData = TeleportFurniture.PairExtraData(entities[i].Id);
            }

            await dbCtx.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        return await ListGrantedAsync(entities, pairDefinitions, ct);
    }

    /// <summary>
    /// A present and the item it holds. The present's id exists only once its row is inserted,
    /// so both rows go in one transaction: the item is never left loose in the inventory, nor
    /// the present empty. Only the present is listed; the held row stays out of every inventory
    /// query until <see cref="UnwrapPresentAsync"/>.
    /// </summary>
    public async Task<FurnitureItemSnapshot> GrantPresentAsync(
        FurnitureDefinitionSnapshot present,
        string presentExtraDataJson,
        FurnitureDefinitionSnapshot content,
        string? contentExtraDataJson,
        CancellationToken ct
    )
    {
        var presentEntity = new FurnitureEntity
        {
            PlayerEntityId = (int)PlayerId,
            FurnitureDefinitionEntityId = present.Id,
            ExtraData = presentExtraDataJson,
        };
        var contentEntity = new FurnitureEntity
        {
            PlayerEntityId = (int)PlayerId,
            FurnitureDefinitionEntityId = content.Id,
            ExtraData = contentExtraDataJson,
        };

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            await using var tx = await dbCtx.Database.BeginTransactionAsync(ct);

            dbCtx.Add(presentEntity);

            await dbCtx.SaveChangesAsync(ct);

            contentEntity.ChestItemEntityId = presentEntity.Id;
            dbCtx.Add(contentEntity);

            await dbCtx.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        var granted = await ListGrantedAsync([presentEntity], [present], ct);

        return granted[0];
    }

    /// <summary>
    /// Releases what a present holds into this inventory. The row is re-owned to this player as
    /// it is released: a present can change hands in a trade, and its contents go to whoever
    /// opens it.
    /// </summary>
    public async Task<FurnitureItemSnapshot?> UnwrapPresentAsync(
        RoomObjectId presentId,
        CancellationToken ct
    )
    {
        FurnitureEntity? row;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            row = await dbCtx
                .Furnitures.Where(x => x.ChestItemEntityId == presentId.Value)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(ct);

            if (row is null)
                return null;

            row.ChestItemEntityId = null;
            row.PlayerEntityId = (int)PlayerId;

            await dbCtx.SaveChangesAsync(ct);
        }

        var definition = GetDefinitionOrThrow(row.FurnitureDefinitionEntityId);
        var listed = await ListGrantedAsync([row], [definition], ct);

        return listed[0];
    }

    /// <summary>
    /// Builds the items for rows just written and tells the owner. A section that is not
    /// loaded is not loaded for this: the client is told its list changed, and the next read
    /// loads the rows with everything else.
    /// </summary>
    private async Task<ImmutableArray<FurnitureItemSnapshot>> ListGrantedAsync(
        List<FurnitureEntity> entities,
        List<FurnitureDefinitionSnapshot> definitions,
        CancellationToken ct
    )
    {
        var ownerName = await GetOwnerNameAsync(ct);
        var items = new List<IFurnitureItem>(entities.Count);

        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];

            items.Add(
                _furnitureLoader.Create(
                    entity.Id,
                    PlayerId,
                    ownerName,
                    definitions[i],
                    entity.ExtraData,
                    entity.CreatedAt
                )
            );
        }

        var snapshots = items.Select(x => x.GetSnapshot()).ToImmutableArray();

        // Only a loaded section is kept in step; an unloaded one reads these rows when it loads.
        if (_state.IsFurnitureReady)
            await AddAsync(items, ct);
        else
            await SendAddedAsync(snapshots, ct);

        MarkUnseen(snapshots);

        return snapshots;
    }

    /// <summary>
    /// Items joined the section. The client refetches its list rather than splicing items in,
    /// so one "list changed" per change covers any number of items.
    /// </summary>
    private Task SendAddedAsync(
        ImmutableArray<FurnitureItemSnapshot> items,
        CancellationToken ct
    ) =>
        items.IsDefaultOrEmpty
            ? Task.CompletedTask
            : GrainFactory.SendComposerToPlayerAsync(
                PlayerId,
                new FurniListInvalidateEventMessageComposer(),
                ct
            );

    /// <summary>Items left the section: one removal each, as one batch to the one player.</summary>
    private Task SendRemovedAsync(ImmutableArray<RoomObjectId> itemIds, CancellationToken ct) =>
        itemIds.IsDefaultOrEmpty
            ? Task.CompletedTask
            : Presence.SendComposerAsync(
                [
                    .. itemIds.Select(itemId => new FurniListRemoveEventMessageComposer
                    {
                        ItemId = itemId,
                    }),
                ],
                ct
            );

    /// <summary>
    /// Furni the player received (bought, rewarded, traded in) is new until they open the tab;
    /// a pick-up from their own room is not, and never comes through here. A rental goes under
    /// the rentals tab. Told, never awaited: this runs where nothing may be awaited (a trade
    /// receipt is interleaved).
    /// </summary>
    private void MarkUnseen(IEnumerable<FurnitureItemSnapshot> snapshots)
    {
        foreach (
            var byCategory in snapshots.GroupBy(snapshot =>
                snapshot.SecondsToExpiration > 0 || snapshot.HasRentPeriodStarted
                    ? UnseenItemCategory.RentedFurni
                    : UnseenItemCategory.OwnedFurni
            )
        )
            UnseenItems
                .AddAsync(
                    byCategory.Key,
                    [.. byCategory.Select(snapshot => snapshot.ItemId.Value)],
                    CancellationToken.None
                )
                .LogAndForget(_logger, "mark new furni for player {PlayerId}", PlayerId);
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
                PlayerId
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
                PlayerId
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

        // Handing items to yourself would change nothing, and a trade never does it.
        if (toPlayerId == PlayerId)
        {
            _logger.LogWarning(
                "Player {PlayerId} was asked to transfer {Count} items to themselves; refused",
                PlayerId,
                itemIds.Length
            );

            return false;
        }

        await EnsureReadyAsync(ct);

        var items = new List<IFurnitureItem>(itemIds.Length);

        foreach (var itemId in itemIds)
        {
            if (!_state.FurnitureById.TryGetValue(itemId, out var item))
            {
                _logger.LogWarning(
                    "Player {PlayerId} no longer holds item {ItemId}; the transfer to {ToPlayerId} is refused",
                    PlayerId,
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
                    && x.PlayerEntityId == (int)PlayerId
                    && x.RoomEntityId == null
                    && x.ChestItemEntityId == null
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
                    PlayerId,
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

        await SendRemovedAsync([.. items.Select(x => x.ItemId)], ct);
        await _inventoryGrain
            .GetInventoryOf(toPlayerId)
            .ReceiveFurnitureAsync([.. items.Select(x => x.GetSnapshot())], ct);

        return true;
    }

    /// <summary>
    /// Items whose rows were just re-owned to this player by another inventory. Runs
    /// interleaved and awaits nothing (see <c>IInventoryGrain.ReceiveFurnitureAsync</c>): a
    /// loaded section lists the items, an unloaded one leaves them to its load, and the client
    /// is told either way.
    /// </summary>
    public void Receive(ImmutableArray<FurnitureItemSnapshot> snapshots)
    {
        if (snapshots.IsDefaultOrEmpty)
            return;

        _state.FurnitureReceiveCount++;

        MarkUnseen(snapshots);

        var added = snapshots;

        if (_state.IsFurnitureReady && _state.OwnerName is { } ownerName)
        {
            added =
            [
                .. snapshots.Where(snapshot =>
                    _state.FurnitureById.TryAdd(
                        snapshot.ItemId,
                        _furnitureLoader.CreateFromFurnitureItemSnapshot(
                            snapshot,
                            PlayerId,
                            ownerName
                        )
                    )
                ),
            ];
        }
        else
        {
            // Not loaded (or loaded without a name, which a load never leaves): the next read
            // loads the rows, these among them.
            _state.IsFurnitureReady = false;
        }

        if (added.IsDefaultOrEmpty)
            return;

        SendAddedAsync(added, CancellationToken.None)
            .LogAndForget(
                _logger,
                "tell player {PlayerId} about {ItemCount} received items",
                PlayerId,
                added.Length
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
            PlayerId
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
            await SendAddedAsync(added.ToImmutable(), ct);

        return added.Count;
    }
}
