using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Database.Entities.Furniture;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomFurniModule(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;

    public Task<ImmutableDictionary<PlayerId, string>> GetAllOwnersAsync(CancellationToken ct) =>
        Task.FromResult(_roomGrain._state.OwnerNamesById.ToImmutableDictionary());

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

        if (
            _roomGrain._itemsLoader.CreateFromFurnitureItemSnapshot(snapshot)
            is not IRoomWallItem item
        )
            return false;

        return await PlaceWallItemAsync(
            ctx,
            item,
            position.X,
            position.Y,
            position.Z,
            position.WallOffset,
            position.Rotation,
            ct
        );
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
