using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Extensions;
using Turbo.Furniture.Custody;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomFurniModule(RoomGrain roomGrain) : RoomGrainComponent(roomGrain)
{
    private readonly List<IRoomPlacementLimit> _placementLimits = [];

    /// <summary>A system that caps its own kind of furni registers here; see <see cref="IRoomPlacementLimit"/>.</summary>
    public void RegisterPlacementLimit(IRoomPlacementLimit limit) => _placementLimits.Add(limit);

    // Read access for the systems that are not this module (wired above all). They ask here
    // instead of reading the room state, so how furni is indexed can change in one place.

    /// <summary>Every floor and wall item in the room.</summary>
    public IReadOnlyCollection<IRoomItem> Items => _roomGrain._state.ItemsById.Values;

    public int ItemCount => _roomGrain._state.ItemsById.Count;

    public bool HasItem(RoomObjectId itemId) => _roomGrain._state.ItemsById.ContainsKey(itemId);

    /// <inheritdoc cref="RoomLiveState.ItemsVersion"/>
    public long ItemsVersion => _roomGrain._state.ItemsVersion;

    public bool TryGetItem(RoomObjectId itemId, [NotNullWhen(true)] out IRoomItem? item) =>
        _roomGrain._state.ItemsById.TryGetValue(itemId, out item);

    public bool TryGetFloorItem(RoomObjectId itemId, [NotNullWhen(true)] out IRoomFloorItem? item)
    {
        item = null;

        if (!TryGetItem(itemId, out var found) || found is not IRoomFloorItem floorItem)
            return false;

        item = floorItem;

        return true;
    }

    /// <summary>The floor items stacked on a tile; none for a tile outside the room.</summary>
    public IEnumerable<IRoomFloorItem> GetFloorItemsOnTile(int tileIdx)
    {
        if (!MapModule.InBounds(tileIdx))
            yield break;

        foreach (var itemId in _roomGrain._state.TileFloorStacks[tileIdx])
        {
            if (TryGetFloorItem(itemId, out var item))
                yield return item;
        }
    }

    /// <summary>Whether this item is the top of the stack on a tile, the one an avatar there stands on.</summary>
    public bool IsHighestOnTile(IRoomFloorItem item, int tileIdx) =>
        MapModule.InBounds(tileIdx)
        && _roomGrain._state.TileHighestFloorItems[tileIdx] == item.ObjectId;

    /// <summary>
    /// Whether some furni in the room lets this player build where a floor item of this size
    /// would stand (see <see cref="IRoomBuildArea"/>). Only asked once room rights said no.
    /// </summary>
    public bool HasBuildAreaRights(
        PlayerId playerId,
        IRoomFloorItem item,
        int x,
        int y,
        Rotation rot
    )
    {
        if (
            !MapModule.GetTileIdForSize(
                x,
                y,
                rot,
                item.Definition.Width,
                item.Definition.Length,
                out var tileIds
            )
        )
            return false;

        return Items.Any(other =>
            other.Logic is IRoomBuildArea area && area.GrantsBuildRights(playerId, tileIds)
        );
    }

    /// <summary>
    /// Asks every registered limit before a new item is placed; a refusal throws. Only the two
    /// placement entry points call it (<see cref="PlaceFloorItemAsync"/>,
    /// <see cref="PlaceWallItemAsync"/>), once the item has its logic, which is how a limit
    /// recognises its own kind. Asked before that, as it used to be, it recognised nothing.
    /// </summary>
    private void EnsureWithinPlacementLimits(IRoomItem item)
    {
        foreach (var limit in _placementLimits)
            limit.EnsureCanPlace(item);
    }

    public ImmutableDictionary<PlayerId, string> GetOwnerNames() =>
        _roomGrain._state.OwnerNamesById.ToImmutableDictionary();

    public Task<int> GetItemCountByOwnerAsync(PlayerId ownerId, CancellationToken ct) =>
        Task.FromResult(Items.Count(item => item.OwnerId == ownerId));

    /// <summary>
    /// The room an item stands in right now, this room or any other; null while it sits in an
    /// inventory. Paired furni (teleporters) use it to find where their pair leads.
    /// </summary>
    public async Task<RoomId?> GetRoomIdOfItemAsync(int itemId, CancellationToken ct)
    {
        if (_roomGrain._state.ItemsById.ContainsKey(itemId))
            return _roomGrain.RoomId;

        await using var dbCtx = await _roomGrain._dbCtxFactory.CreateDbContextAsync(ct);

        var roomId = await dbCtx
            .Furnitures.AsNoTracking()
            .Where(x => x.Id == itemId)
            .Select(x => x.RoomEntityId)
            .FirstOrDefaultAsync(ct);

        return roomId is > 0 ? RoomId.Parse(roomId.Value) : null;
    }

    /// <summary>
    /// Creates a brand-new wall item directly in the room, for furniture the room produces
    /// itself (a note pinned to a post-it wall), through the furniture custody under
    /// <paramref name="cause"/>. The row is written first so the object id is the database id;
    /// the persistence grain keeps it updated from then on like any other item.
    /// </summary>
    public async Task<bool> CreateWallItemAsync(
        ActionContext ctx,
        FurnitureDefinitionSnapshot definition,
        PlayerId ownerId,
        WallPosition position,
        string extraDataJson,
        StuffDataSnapshot stuffData,
        MovementCause cause,
        CancellationToken ct
    )
    {
        var created = (
            await _roomGrain._custody.CreateInRoomAsync(
                _roomGrain.RoomId,
                ownerId,
                [
                    new FurniToPlace(
                        new FurniToMake(definition.Id, extraDataJson),
                        new RoomPosition(
                            position.X,
                            position.Y,
                            position.Z,
                            position.Rotation,
                            position.WallOffset
                        )
                    ),
                ],
                cause,
                ct
            )
        )[0];

        // No owner name: the room fills it in from its own table as the item attaches.
        var snapshot = new FurnitureItemSnapshot
        {
            ItemId = created.Id,
            SpriteId = definition.SpriteId,
            OwnerId = ownerId,
            OwnerName = string.Empty,
            Definition = definition,
            StuffData = stuffData,
            ExtraData = created.ExtraDataJson ?? string.Empty,
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = _roomGrain.RoomId,
        };

        var made = _roomGrain._itemsLoader.CreateFromFurnitureItemSnapshot(snapshot);
        var placed = false;

        try
        {
            if (made is not IRoomWallItem item)
                return false;

            placed = await PlaceWallItemAsync(
                ctx,
                item,
                position.X,
                position.Y,
                position.Z,
                position.WallOffset,
                position.Rotation,
                ct
            );

            return placed;
        }
        finally
        {
            // The row already says the item is in this room, so an item the room refused (a
            // placement limit, a bad spot) would appear on the next load. It goes again, under
            // the same cause after its creation.
            if (!placed)
                await _roomGrain
                    ._grainFactory.GetRoomPersistenceGrain(_roomGrain.RoomId)
                    .WriteUseAsync(
                        _roomGrain.RoomId,
                        new UsedUpItem(made.GetSnapshot(), cause.StartingAt(cause.FirstSeq + 1)),
                        [],
                        ct
                    );
        }
    }

    internal async Task EnsureFurniLoadedAsync(CancellationToken ct)
    {
        if (_roomGrain._state.IsFurniLoaded)
            return;

        var (floorItems, wallItems, ownerNames) = await _roomGrain._itemsLoader.LoadByRoomIdAsync(
            _roomGrain.RoomId,
            ct
        );

        foreach (var (id, name) in ownerNames)
            _roomGrain._state.OwnerNamesById.TryAdd(id, name);

        _roomGrain._state.IsTileComputationPaused = true;

        foreach (var item in floorItems)
            await ObjectModule.AttatchObjectAsync(item, ct);

        _roomGrain._state.IsTileComputationPaused = false;

        MapModule.ComputeAllTiles();
        _roomGrain._state.DirtyHeightTileIds.Clear();
        _roomGrain._state.DirtyHeightSentValues.Clear();

        foreach (var item in wallItems)
            await ObjectModule.AttatchObjectAsync(item, ct);

        CaptureLoadedItems();

        _roomGrain._state.IsFurniLoaded = true;
    }

    /// <summary>
    /// Remembers every furni as it was loaded, for a wired menu rollback. A temporary furni has
    /// no row to put back.
    /// </summary>
    internal void CaptureLoadedItems()
    {
        var snapshots = _roomGrain._state.LoadedItemSnapshots;

        snapshots.Clear();

        foreach (var item in _roomGrain._state.ItemsById.Values)
        {
            if (!item.IsTemporary)
                snapshots[item.ObjectId] = item.GetSnapshot();
        }
    }

    public Task<RoomItemSnapshot?> GetItemSnapshotByIdAsync(
        RoomObjectId objectId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _roomGrain._state.ItemsById.TryGetValue(objectId, out var item)
                ? item.GetSnapshot()
                : null
        );
}
