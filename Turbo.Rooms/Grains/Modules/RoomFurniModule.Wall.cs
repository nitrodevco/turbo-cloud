using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums;
using Turbo.Logging;
using Turbo.Primitives.Actor;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Furniture.Wall;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomFurniModule
{
    public async Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct)
    {
        if (!_state.WallItemsById.TryAdd(item.Id, item))
            return false;

        void func(long itemId) => _state.DirtyItemIds.Add(itemId);

        item.SetAction(func);

        await AttatchWallLogicIfNeededAsync(item, ct);

        _ = _roomGrain.SendComposerToRoomAsync(item.GetAddComposer(), ct);

        return true;
    }

    public async Task<bool> MoveWallItemByIdAsync(
        ActorContext ctx,
        long itemId,
        string newLocation,
        CancellationToken ct
    )
    {
        if (!_state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        if (item.WallLocation.Equals(newLocation, StringComparison.OrdinalIgnoreCase))
            return false;

        item.SetPosition(newLocation);

        _ = _roomGrain.SendComposerToRoomAsync(item.GetUpdateComposer(), ct);

        await item.Logic.OnMoveAsync(ctx, ct);

        return true;
    }

    public async Task<bool> RemoveWallItemByIdAsync(
        ActorContext ctx,
        long itemId,
        CancellationToken ct
    )
    {
        if (!_state.WallItemsById.Remove(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        _ = _roomGrain.SendComposerToRoomAsync(item.GetRemoveComposer(ctx.PlayerId), ct);

        item.SetAction(null);

        await item.Logic.OnPickupAsync(ctx, ct);

        return true;
    }

    public async Task<bool> UseWallItemByIdAsync(
        ActorContext ctx,
        long itemId,
        int param,
        CancellationToken ct
    )
    {
        if (!_state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        await item.Logic.OnUseAsync(ctx, param, ct);

        return true;
    }

    public async Task<bool> ClickWallItemByIdAsync(
        ActorContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    )
    {
        if (!_state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        await item.Logic.OnClickAsync(ctx, param, ct);

        return true;
    }

    public async Task<bool> ValidateWallItemPlacementAsync(
        ActorContext ctx,
        long itemId,
        string newLocation
    )
    {
        return true;
    }

    public Task<RoomWallItemSnapshot?> GetWallItemSnapshotByIdAsync(
        long itemId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.WallItemsById.TryGetValue(itemId, out var item) ? item.GetSnapshot() : null
        );

    private async Task AttatchWallLogicIfNeededAsync(IRoomWallItem item, CancellationToken ct)
    {
        if (item.Logic is not null)
            return;

        var logicType = item.Definition.LogicName;
        var ctx = new RoomWallItemContext(_roomGrain, this, item);
        var logic = _furnitureLogicFactory.CreateLogicInstance(logicType, ctx);

        if (logic is not IFurnitureWallLogic wallLogic)
            throw new TurboException(TurboErrorCodeEnum.InvalidWallLogic);

        item.SetLogic(wallLogic);

        await logic.OnAttachAsync(ct);
    }
}
