using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Furniture;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
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

public sealed partial class RoomFurniModule(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;

    private readonly List<IRoomPlacementLimit> _placementLimits = [];

    /// <summary>A system that caps its own kind of furni registers here; see <see cref="IRoomPlacementLimit"/>.</summary>
    public void RegisterPlacementLimit(IRoomPlacementLimit limit) => _placementLimits.Add(limit);

    // Read access for the systems that are not this module (wired above all). They ask here
    // instead of reading the room state, so how furni is indexed can change in one place.

    /// <summary>Every floor and wall item in the room.</summary>
    public IReadOnlyCollection<IRoomItem> Items => _roomGrain._state.ItemsById.Values;

    public int ItemCount => _roomGrain._state.ItemsById.Count;

    public bool HasItem(RoomObjectId itemId) => _roomGrain._state.ItemsById.ContainsKey(itemId);

    public bool TryGetItem(RoomObjectId itemId, out IRoomItem item)
    {
        if (_roomGrain._state.ItemsById.TryGetValue(itemId, out var found))
        {
            item = found;

            return true;
        }

        item = null!;

        return false;
    }

    public bool TryGetFloorItem(RoomObjectId itemId, out IRoomFloorItem item)
    {
        item = null!;

        if (!TryGetItem(itemId, out var found) || found is not IRoomFloorItem floorItem)
            return false;

        item = floorItem;

        return true;
    }

    /// <summary>The floor items stacked on a tile; none for a tile outside the room.</summary>
    public IEnumerable<IRoomFloorItem> GetFloorItemsOnTile(int tileIdx)
    {
        if (!_roomGrain.MapModule.InBounds(tileIdx))
            yield break;

        foreach (var itemId in _roomGrain._state.TileFloorStacks[tileIdx])
        {
            if (TryGetFloorItem(itemId, out var item))
                yield return item;
        }
    }

    /// <summary>Whether this item is the top of the stack on a tile, the one an avatar there stands on.</summary>
    public bool IsHighestOnTile(IRoomFloorItem item, int tileIdx) =>
        _roomGrain.MapModule.InBounds(tileIdx)
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
            !_roomGrain.MapModule.GetTileIdForSize(
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

    public Task<ImmutableDictionary<PlayerId, string>> GetAllOwnersAsync(CancellationToken ct) =>
        Task.FromResult(_roomGrain._state.OwnerNamesById.ToImmutableDictionary());

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
    /// itself (a note pinned to a post-it wall). The row is written first so the object id is the
    /// database id; the persistence grain keeps it updated from then on like any other item.
    /// </summary>
    public async Task<bool> CreateWallItemAsync(
        ActionContext ctx,
        FurnitureDefinitionSnapshot definition,
        PlayerId ownerId,
        WallPosition position,
        string extraDataJson,
        StuffDataSnapshot stuffData,
        CancellationToken ct
    )
    {
        await using var dbCtx = await _roomGrain._dbCtxFactory.CreateDbContextAsync(ct);

        var entity = new FurnitureEntity
        {
            PlayerEntityId = ownerId.Value,
            FurnitureDefinitionEntityId = definition.Id,
            RoomEntityId = _roomGrain.RoomId.Value,
            X = position.X,
            Y = position.Y,
            Z = position.Z,
            Rotation = position.Rotation,
            WallOffset = position.WallOffset,
            ExtraData = extraDataJson,
        };

        dbCtx.Add(entity);

        await dbCtx.SaveChangesAsync(ct);

        var snapshot = new FurnitureItemSnapshot
        {
            ItemId = entity.Id,
            SpriteId = definition.SpriteId,
            OwnerId = ownerId,
            OwnerName = string.Empty,
            Definition = definition,
            StuffData = stuffData,
            ExtraData = extraDataJson,
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = _roomGrain.RoomId,
        };

        var placed = false;

        try
        {
            if (
                _roomGrain._itemsLoader.CreateFromFurnitureItemSnapshot(snapshot)
                is not IRoomWallItem item
            )
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
            // placement limit, a bad spot) would appear on the next load. It goes again.
            if (!placed)
                await _roomGrain
                    ._grainFactory.GetRoomPersistenceGrain(_roomGrain.RoomId)
                    .EnqueueDeletedItemAsync(_roomGrain.RoomId, entity.Id, ct);
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
            await _roomGrain.ObjectModule.AttatchObjectAsync(item, ct);

        _roomGrain._state.IsTileComputationPaused = false;

        _roomGrain.MapModule.ComputeAllTiles();
        _roomGrain._state.DirtyHeightTileIds.Clear();

        foreach (var item in wallItems)
            await _roomGrain.ObjectModule.AttatchObjectAsync(item, ct);

        _roomGrain._state.IsFurniLoaded = true;
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
