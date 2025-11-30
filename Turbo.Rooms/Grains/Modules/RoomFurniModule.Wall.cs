using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums;
using Turbo.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Object.Furniture.Wall;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomFurniModule
{
    public async Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct)
    {
        if (!_state.WallItemsById.TryAdd(item.ObjectId.Value, item))
            return false;

        void func(RoomObjectId objectId) => _state.DirtyItemIds.Add(objectId.Value);

        item.SetAction(func);

        await AttatchWallLogicIfNeededAsync(item, ct);

        _ = _roomGrain.SendComposerToRoomAsync(item.GetAddComposer(), ct);

        return true;
    }

    public async Task<bool> MoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        string newLocation,
        CancellationToken ct
    )
    {
        if (!_state.WallItemsById.TryGetValue(objectId.Value, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        if (item.WallLocation.Equals(newLocation, StringComparison.OrdinalIgnoreCase))
            return false;

        item.SetPosition(newLocation);

        _ = _roomGrain.SendComposerToRoomAsync(item.GetUpdateComposer(), ct);

        await item.Logic.OnMoveAsync(ctx, ct);

        return true;
    }

    public async Task<bool> RemoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct
    )
    {
        if (!_state.WallItemsById.Remove(objectId.Value, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        _ = _roomGrain.SendComposerToRoomAsync(item.GetRemoveComposer(ctx.PlayerId), ct);

        item.SetAction(null);

        await item.Logic.OnDetachAsync(ct);

        return true;
    }

    public async Task<bool> UseWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_state.WallItemsById.TryGetValue(objectId.Value, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        await item.Logic.OnUseAsync(ctx, param, ct);

        return true;
    }

    public async Task<bool> ClickWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_state.WallItemsById.TryGetValue(objectId.Value, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        await item.Logic.OnClickAsync(ctx, param, ct);

        return true;
    }

    public Task<bool> ValidateWallItemPlacementAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        string newLocation
    ) => Task.FromResult(true);

    public Task<RoomWallItemSnapshot?> GetWallItemSnapshotByIdAsync(
        RoomObjectId objectId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.WallItemsById.TryGetValue(objectId.Value, out var item)
                ? item.GetSnapshot()
                : null
        );

    private async Task AttatchWallLogicIfNeededAsync(IRoomWallItem item, CancellationToken ct)
    {
        if (item.Logic is not null)
            return;

        var logicType = item.Definition.LogicName;
        var ctx = new RoomWallItemContext(_roomGrain, this, item);
        var logic = _logicFactory.CreateLogicInstance(logicType, ctx);

        if (logic is not IFurnitureWallLogic wallLogic)
            throw new TurboException(TurboErrorCodeEnum.InvalidLogic);

        item.SetLogic(wallLogic);

        await logic.OnAttachAsync(ct);
    }
}
