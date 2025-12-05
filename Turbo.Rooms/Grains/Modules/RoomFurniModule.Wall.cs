using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Enums;
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

        item.SetAction(objectId => ProcessDirtyWallItem(objectId.Value, ct));

        await AttatchWallLogicIfNeededAsync(item, ct);

        _ = _roomGrain.SendComposerToRoomAsync(item.GetAddComposer(), ct);

        return true;
    }

    public async Task<bool> PlaceWallItemAsync(
        ActionContext ctx,
        FurnitureWallItemSnapshot item,
        int newX,
        int newY,
        double newZ,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    )
    {
        var roomItem = _itemsLoader.CreateFromFurnitureItemSnapshot(item);

        if (roomItem is not IRoomWallItem wallItem)
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        wallItem.SetPosition(newX, newY, newZ, wallOffset, rot);

        return await AddWallItemAsync(wallItem, ct);
    }

    public async Task<bool> MoveWallItemByIdAsync(
        ActionContext ctx,
        int itemId,
        int newX,
        int newY,
        double newZ,
        int wallOffset,
        Rotation newRot,
        CancellationToken ct
    )
    {
        if (!_state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        if (
            item.X == newX
            && item.Y == newY
            && item.Z == newZ
            && item.WallOffset == wallOffset
            && item.Rotation == newRot
        )
            return false;

        item.SetPosition(newX, newY, newZ, wallOffset, newRot);

        _ = _roomGrain.SendComposerToRoomAsync(item.GetUpdateComposer(), ct);

        await item.Logic.OnMoveAsync(ctx, ct);

        return true;
    }

    public async Task<bool> RemoveWallItemByIdAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct
    )
    {
        if (!_state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        await item.Logic.OnDetachAsync(ct);

        item.SetAction(null);

        _ = _roomGrain.SendComposerToRoomAsync(item.GetRemoveComposer(ctx.PlayerId), ct);

        await FlushDirtyWallItemNowAsync(itemId, ct);

        _state.WallItemsById.Remove(itemId);

        return true;
    }

    public async Task<bool> UseWallItemByIdAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        await item.Logic.OnUseAsync(ctx, param, ct);

        return true;
    }

    public async Task<bool> ClickWallItemByIdAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        await item.Logic.OnClickAsync(ctx, param, ct);

        return true;
    }

    public Task<bool> ValidateWallItemPlacementAsync(
        ActionContext ctx,
        int itemId,
        int newX,
        int newY,
        double newZ,
        int wallOffset,
        Rotation newRot
    ) => Task.FromResult(true);

    public Task<bool> ValidateNewWallItemPlacementAsync(
        ActionContext ctx,
        FurnitureWallItemSnapshot item,
        int newX,
        int newY,
        double newZ,
        int wallOffset,
        Rotation newRot
    ) => Task.FromResult(true);

    public Task<ImmutableArray<RoomWallItemSnapshot>> GetAllWallItemSnapshotsAsync(
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.WallItemsById.Values.Select(x => x.GetSnapshot()).ToImmutableArray()
        );

    public Task<RoomWallItemSnapshot?> GetWallItemSnapshotByIdAsync(
        RoomObjectId objectId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.WallItemsById.TryGetValue(objectId.Value, out var item)
                ? item.GetSnapshot()
                : null
        );

    private void ProcessDirtyWallItem(int itemId, CancellationToken ct)
    {
        _state.DirtyItemIds.Add(itemId);
    }

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

    private async Task FlushDirtyWallItemNowAsync(long itemId, CancellationToken ct)
    {
        try
        {
            if (!_state.WallItemsById.TryGetValue(itemId, out var item))
                throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

            _state.DirtyItemIds.Remove(itemId);

            var snapshot = item.GetSnapshot();

            using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var entity = await dbCtx.Furnitures.SingleAsync(
                x => x.Id == snapshot.ObjectId.Value,
                ct
            );

            entity.X = snapshot.X;
            entity.Y = snapshot.Y;
            entity.Z = snapshot.Z;
            entity.Rotation = snapshot.Rotation;
            entity.WallOffset = snapshot.WallOffset;
            entity.StuffData = snapshot.StuffData;

            await dbCtx.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            // TODO output log
        }
    }
}
