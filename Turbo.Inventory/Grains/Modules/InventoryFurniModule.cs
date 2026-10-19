using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Furniture.Custody;
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
using Turbo.Primitives.Movements;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Inventory.Grains.Modules;

/// <summary>
/// The furniture a player keeps in the inventory (rows in no room). Same rules as the pet and
/// bot modules: the section loads on first use, the row is written before the list changes,
/// and the presence is told once per change, not once per item. The furniture custody writes
/// every row, under the cause the caller gives, a post-it pad's sheet count included.
/// </summary>
internal sealed class InventoryFurniModule(
    InventoryGrain inventoryGrain,
    InventoryLiveState liveState,
    IFurnitureCustody custody,
    IInventoryFurnitureLoader furnitureLoader,
    IFurnitureDefinitionProvider definitionProvider,
    ICatalogService catalogService,
    ILogger logger
) : InventoryGrainComponent(inventoryGrain)
{
    private readonly InventoryLiveState _state = liveState;
    private readonly IFurnitureCustody _custody = custody;
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
        MovementCause cause,
        CancellationToken ct
    )
    {
        await EnsureReadyAsync(ct);

        if (!_state.FurnitureById.TryGetValue(itemId, out var item))
            return null;

        var snapshot = item.GetSnapshot();

        if (!await _custody.DestroyInInventoryAsync(PlayerId, snapshot, cause, ct))
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

        return snapshot;
    }

    /// <summary>
    /// One sheet off a post-it pad held here. A pad of several sheets keeps one fewer: its row
    /// first, then its item, and the client sets the count it is told (Flash
    /// <c>IncomingMessages.onPostItPlaced</c> -> <c>FurniModel.updatePostItCount</c>), which it
    /// applies itself, so no list update goes with it. The last sheet is the pad itself, used up
    /// as a room paper is (<see cref="ConsumeAsync"/>). Null when no pad of that id is held here.
    /// </summary>
    public async Task<PostItSheetTakenSnapshot?> TakePostItSheetAsync(
        RoomObjectId padId,
        MovementCause cause,
        CancellationToken ct
    )
    {
        await EnsureReadyAsync(ct);

        if (
            !_state.FurnitureById.TryGetValue(padId, out var item)
            || !PostItPads.IsPad(item.Definition)
        )
        {
            _logger.LogWarning(
                "Player {PlayerId} holds no post-it pad {ItemId}; no sheet taken",
                PlayerId,
                padId
            );

            return null;
        }

        var pad = item.GetSnapshot();
        var sheets = PostItPads.SheetsOf(pad.StuffData);

        if (sheets <= 1)
            return await ConsumeAsync(padId, cause, ct) is null
                ? null
                : new PostItSheetTakenSnapshot { Pad = pad, SheetsLeft = 0 };

        return await SetPostItSheetsAsync(item, sheets - 1, ct)
            ? new PostItSheetTakenSnapshot { Pad = pad, SheetsLeft = sheets - 1 }
            : null;
    }

    /// <summary>
    /// A sheet <see cref="TakePostItSheetAsync"/> took, given back: the pad counts it again while it
    /// is still here, and otherwise (it went with its last sheet, or was traded or placed since) a
    /// pad of one sheet of its kind is made in its place.
    /// </summary>
    public async Task ReturnPostItSheetAsync(
        PostItSheetTakenSnapshot taken,
        MovementCause cause,
        CancellationToken ct
    )
    {
        await EnsureReadyAsync(ct);

        if (
            !taken.UsedUpPad
            && _state.FurnitureById.TryGetValue(taken.Pad.ItemId, out var item)
            && PostItPads.IsPad(item.Definition)
            && await SetPostItSheetsAsync(
                item,
                PostItPads.SheetsOf(item.GetSnapshot().StuffData) + 1,
                ct
            )
        )
            return;

        await GrantAsync([(taken.Pad.Definition, PostItPads.ExtraData(1))], cause, ct);
    }

    /// <summary>
    /// A pad now has <paramref name="sheets"/> left: written to its row, then to its item, then
    /// told to the client. False when the row is no longer this inventory's.
    /// </summary>
    private async Task<bool> SetPostItSheetsAsync(
        IFurnitureItem item,
        int sheets,
        CancellationToken ct
    )
    {
        var extraDataJson = PostItPads.ExtraData(sheets);

        if (!await _custody.SetExtraDataInInventoryAsync(PlayerId, item.ItemId, extraDataJson, ct))
        {
            _logger.LogWarning(
                "Post-it pad {ItemId} is listed in the inventory of player {PlayerId} but has no row there; its sheets were not counted",
                item.ItemId,
                PlayerId
            );

            return false;
        }

        _state.FurnitureById[item.ItemId] = _furnitureLoader.Create(
            item.ItemId,
            PlayerId,
            item.OwnerName,
            item.Definition,
            extraDataJson,
            item.GetSnapshot().CreatedAtUtc
        );

        await Presence.SendComposerAsync(
            new PostItPlacedEventMessageComposer { ItemId = item.ItemId, ItemsLeft = sheets },
            ct
        );

        return true;
    }

    /// <summary>
    /// Creates one row per definition and lists the items. One insert and one presence call,
    /// however many items a purchase grants. A section that is not loaded is not loaded for
    /// this: the rows are written, a client holding the list is sent the items, and the next
    /// read loads them with everything else.
    /// </summary>
    public async Task<ImmutableArray<FurnitureItemSnapshot>> GrantAsync(
        IReadOnlyList<(FurnitureDefinitionSnapshot Definition, string? ExtraDataJson)> grants,
        MovementCause cause,
        CancellationToken ct
    )
    {
        if (grants.Count == 0)
            return [];

        var rows = await _custody.CreateInInventoryAsync(
            PlayerId,
            [.. grants.Select(grant => new FurniToMake(grant.Definition.Id, grant.ExtraDataJson))],
            cause,
            ct
        );

        return await ListGrantedAsync(rows, [.. grants.Select(grant => grant.Definition)], ct);
    }

    /// <summary>
    /// Teleporters, each unit a linked pair: two rows, each naming the other in its
    /// <c>room_linker</c> section; no half is ever left unlinked
    /// (<see cref="IFurnitureCustody.CreateTeleportPairsInInventoryAsync"/>).
    /// </summary>
    public async Task<ImmutableArray<FurnitureItemSnapshot>> GrantTeleportPairsAsync(
        IReadOnlyList<FurnitureDefinitionSnapshot> definitions,
        MovementCause cause,
        CancellationToken ct
    )
    {
        if (definitions.Count == 0)
            return [];

        var rows = await _custody.CreateTeleportPairsInInventoryAsync(
            PlayerId,
            [.. definitions.Select(definition => definition.Id)],
            cause,
            ct
        );

        return await ListGrantedAsync(
            rows,
            [.. definitions.SelectMany(definition => new[] { definition, definition })],
            ct
        );
    }

    /// <summary>
    /// A present and the item it holds, both or neither: the item is never left loose in the
    /// inventory, nor the present empty. Only the present is listed; the held row stays out of
    /// every inventory query until <see cref="UnwrapPresentAsync"/>.
    /// </summary>
    public async Task<FurnitureItemSnapshot> GrantPresentAsync(
        FurnitureDefinitionSnapshot present,
        string presentExtraDataJson,
        FurnitureDefinitionSnapshot content,
        string? contentExtraDataJson,
        MovementCause cause,
        CancellationToken ct
    )
    {
        var presentRow = await _custody.CreatePresentInInventoryAsync(
            PlayerId,
            new FurniToMake(present.Id, presentExtraDataJson),
            new FurniToMake(content.Id, contentExtraDataJson),
            cause,
            ct
        );

        var granted = await ListGrantedAsync([presentRow], [present], ct);

        return granted[0];
    }

    /// <summary>
    /// Releases what a present holds into this inventory. The row is re-owned to this player as
    /// it is released: a present can change hands in a trade, and its contents go to whoever
    /// opens it.
    /// </summary>
    public async Task<FurnitureItemSnapshot?> UnwrapPresentAsync(
        RoomObjectId presentId,
        MovementCause cause,
        CancellationToken ct
    )
    {
        var row = await _custody.TakeOutOfPresentAsync(PlayerId, presentId, cause, ct);

        if (row is null)
            return null;

        var definition = GetDefinitionOrThrow(row.DefinitionId);
        var listed = await ListGrantedAsync([row], [definition], ct);

        return listed[0];
    }

    /// <summary>
    /// Builds the items for rows just written and tells the owner. A section that is not
    /// loaded is not loaded for this: a client holding the list is sent the items, and the next
    /// read loads the rows with everything else.
    /// </summary>
    private async Task<ImmutableArray<FurnitureItemSnapshot>> ListGrantedAsync(
        IReadOnlyList<CustodyFurni> rows,
        IReadOnlyList<FurnitureDefinitionSnapshot> definitions,
        CancellationToken ct
    )
    {
        var ownerName = await GetOwnerNameAsync(ct);
        var items = new List<IFurnitureItem>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            items.Add(
                _furnitureLoader.Create(
                    row.Id,
                    PlayerId,
                    ownerName,
                    definitions[i],
                    row.ExtraDataJson,
                    row.CreatedAt
                )
            );
        }

        var snapshots = items.Select(x => x.GetSnapshot()).ToImmutableArray();

        // Only a loaded section is kept in step; an unloaded one reads these rows when it loads.
        if (_state.IsFurnitureReady)
            await AddAsync(items, ct);
        else
            await SendAddedOrUpdatedAsync(snapshots, ct);

        MarkUnseen(snapshots);

        return snapshots;
    }

    /// <summary>
    /// The whole list, in fragments, to the session that asked; from then on that session is
    /// kept in step item by item. A receive that lands while the rows are read may be missing
    /// from what was read: that list is sent as it is, then called stale, and the client asks
    /// again rather than being kept in step with a list that lacks an item.
    /// </summary>
    public async Task SendListAsync(SessionKey sessionKey, CancellationToken ct)
    {
        var items = await GetAllAsync(ct);
        var composers = ComposerFragments.Build(
            items,
            Config.FurnitureInventoryFragmentSize,
            (total, current, fragment) =>
                new FurniListEventMessageComposer
                {
                    TotalFragments = total,
                    CurrentFragment = current,
                    Items = fragment,
                }
        );

        // EnsureReadyAsync leaves the section unready only when a receive raced the read.
        if (_state.IsFurnitureReady)
        {
            _state.FurnitureListSessionKey = sessionKey;
        }
        else
        {
            _state.FurnitureListSessionKey = SessionKey.Invalid;
            composers.Add(new FurniListInvalidateEventMessageComposer());
        }

        _state.IsFurnitureListHolderKnown = true;

        await Presence.SendComposerAsync(composers, ct);
    }

    /// <summary>
    /// Items joined the section or changed in it. Only a client holding the list is told, with
    /// the items themselves; one that has not asked for it yet gets the rows with the rest when
    /// it does. The session that asked having gone, no later one is told until it asks itself.
    /// An activation that does not know whether a client holds the list (the one before it was
    /// collected while the player stayed online) calls the list stale once instead, so a client
    /// holding it asks again when the inventory next shows it (Flash <c>onFurniListInvalidate</c>).
    /// </summary>
    private async Task SendAddedOrUpdatedAsync(
        ImmutableArray<FurnitureItemSnapshot> items,
        CancellationToken ct
    )
    {
        var sessionKey = _state.FurnitureListSessionKey;

        if (items.IsDefaultOrEmpty)
            return;

        if (sessionKey == SessionKey.Invalid)
        {
            if (_state.IsFurnitureListHolderKnown)
                return;

            _state.IsFurnitureListHolderKnown = true;

            await Presence.SendComposerAsync(new FurniListInvalidateEventMessageComposer(), ct);

            return;
        }

        var sent = await Presence.TrySendComposerToSessionAsync(
            sessionKey,
            new FurniListAddOrUpdateEventMessageComposer { Items = items },
            ct
        );

        if (!sent && _state.FurnitureListSessionKey == sessionKey)
            _state.FurnitureListSessionKey = SessionKey.Invalid;
    }

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
        MovementCause cause,
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

        var granted = await GrantAsync([(definition, extraDataJson)], cause, ct);

        return granted.Length > 0 ? granted[0] : null;
    }

    /// <summary>A limited edition item: the serial travels in the stuff section of its extra data.</summary>
    public async Task GrantLimitedAsync(
        int catalogProductId,
        int serialNumber,
        int seriesSize,
        MovementCause cause,
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

        await GrantAsync(
            [(GetDefinitionOrThrow(product.FurniDefinitionId), extraDataJson)],
            cause,
            ct
        );
    }

    /// <summary>
    /// Moves items held here to another player (a completed trade). All or nothing: the rows
    /// change owner in one guarded statement, then both lists and both clients follow.
    /// </summary>
    public async Task<bool> TransferAsync(
        ImmutableArray<RoomObjectId> itemIds,
        PlayerId toPlayerId,
        MovementCause cause,
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

        if (
            !await _custody.TransferBetweenInventoriesAsync(
                PlayerId,
                toPlayerId,
                [.. items.Select(x => x.GetSnapshot())],
                cause,
                ct
            )
        )
        {
            // The list and the rows disagree; reload rather than keep trading on it.
            _state.IsFurnitureReady = false;

            return false;
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
    /// loaded section lists the items, an unloaded one leaves them to its load, and a client
    /// holding the list is sent them either way.
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

        SendAddedOrUpdatedAsync(added, CancellationToken.None)
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
            await SendAddedOrUpdatedAsync(added.ToImmutable(), ct);

        return added.Count;
    }
}
