using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Object.Furniture.Wall;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomFurniModule
{
    public async Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct)
    {
        if (!await AttatchWallItemAsync(item, ct) || !_roomGrain.MapModule.AddWallItem(item))
            return false;

        return true;
    }

    public async Task<bool> PlaceWallItemAsync(
        ActionContext ctx,
        IRoomWallItem item,
        int x,
        int y,
        double z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (
            !await AttatchWallItemAsync(item, ct)
            || !_roomGrain.MapModule.PlaceWallItem(item, x, y, z, rot, wallOffset)
        )
            return false;

        await item.Logic.OnPlaceAsync(ctx, ct);

        item.MarkDirty();

        await _roomGrain.SendComposerToRoomAsync(item.GetAddComposer());
        return true;
    }

    public async Task<bool> MoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        double z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (!_roomGrain._state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        if (!_roomGrain.MapModule.MoveWallItemItem(item, x, y, z, rot, wallOffset))
            return false;

        await _roomGrain.SendComposerToRoomAsync(item.GetUpdateComposer());

        await item.Logic.OnMoveAsync(ctx, -1, ct);

        return true;
    }

    public async Task<RoomWallItemSnapshot?> RemoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int pickerId = -1
    )
    {
        if (!_roomGrain._state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        if (pickerId == -1)
            pickerId = item.OwnerId;

        if (!_roomGrain.MapModule.RemoveWallItem(item))
            return null;

        await _roomGrain.SendComposerToRoomAsync(item.GetRemoveComposer(pickerId));

        await item.Logic.OnDetachAsync(ct);
        await item.Logic.OnPickupAsync(ctx, ct);

        item.SetOwnerId(pickerId);
        item.SetAction(null);

        _roomGrain._state.WallItemsById.Remove(itemId);

        var snapshot = item.GetSnapshot();

        await _roomGrain
            ._grainFactory.GetRoomPersistenceGrain(_roomGrain._state.RoomId)
            .EnqueueDirtyItemAsync(_roomGrain._state.RoomId, snapshot, ct, true);

        return snapshot;
    }

    public async Task<bool> UseWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_roomGrain._state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        await item.Logic.OnUseAsync(ctx, param, ct);

        return true;
    }

    public async Task<bool> ClickWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_roomGrain._state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        await item.Logic.OnClickAsync(ctx, param, ct);

        return true;
    }

    public Task<bool> ValidateWallItemPlacementAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        double z,
        int wallOffset,
        Rotation rot
    ) => Task.FromResult(true);

    public Task<bool> ValidateNewWallItemPlacementAsync(
        ActionContext ctx,
        IRoomWallItem item,
        int x,
        int y,
        double z,
        int wallOffset,
        Rotation rot
    ) => Task.FromResult(true);

    public Task<ImmutableArray<RoomWallItemSnapshot>> GetAllWallItemSnapshotsAsync(
        CancellationToken ct
    ) =>
        Task.FromResult(
            _roomGrain._state.WallItemsById.Values.Select(x => x.GetSnapshot()).ToImmutableArray()
        );

    public Task<RoomWallItemSnapshot?> GetWallItemSnapshotByIdAsync(
        RoomObjectId objectId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _roomGrain._state.WallItemsById.TryGetValue(objectId, out var item)
                ? item.GetSnapshot()
                : null
        );

    private async Task<bool> AttatchWallItemAsync(IRoomWallItem item, CancellationToken ct)
    {
        if (!_roomGrain._state.WallItemsById.TryAdd(item.ObjectId, item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        if (!_roomGrain._state.OwnerNamesById.TryGetValue(item.OwnerId, out string? value))
        {
            var ownerName = await _roomGrain
                ._grainFactory.GetPlayerDirectoryGrain()
                .GetPlayerNameAsync(item.OwnerId, ct);

            value = ownerName;
            _roomGrain._state.OwnerNamesById[item.OwnerId] = value;
        }

        item.SetOwnerName(value ?? string.Empty);
        item.SetAction(objectId => _roomGrain._state.DirtyWallItemIds.Add(objectId));
        await AttatchWallLogicIfNeededAsync(item, ct);

        return true;
    }

    private async Task AttatchWallLogicIfNeededAsync(IRoomWallItem item, CancellationToken ct)
    {
        if (item.Logic is not null)
            return;

        var logicType = item.Definition.LogicName;
        var ctx = new RoomWallItemContext(_roomGrain, this, item);
        var logic = _roomGrain._logicProvider.CreateLogicInstance(logicType, ctx);

        if (logic is not IFurnitureWallLogic wallLogic)
            throw new TurboException(TurboErrorCodeEnum.InvalidLogic);

        item.SetLogic(wallLogic);

        await logic.OnAttachAsync(ct);
    }
}
