using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomActionModule
{
    public Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct) =>
        _furniModule.AddWallItemAsync(item, ct);

    public async Task<bool> PlaceWallItemAsync(
        ActionContext ctx,
        FurnitureItemSnapshot snapshot,
        int x,
        int y,
        double z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (!await _securityModule.CanPlaceFurniAsync(ctx))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToPlaceFurni);

        var item = _roomGrain._itemsLoader.CreateFromFurnitureItemSnapshot(snapshot);

        if (item is not IRoomWallItem wallItem)
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        if (
            !await _furniModule.ValidateNewWallItemPlacementAsync(
                ctx,
                wallItem,
                x,
                y,
                z,
                wallOffset,
                rot
            )
        )
            throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

        if (!await _furniModule.PlaceWallItemAsync(ctx, wallItem, x, y, z, wallOffset, rot, ct))
            return false;

        var inventory = _roomGrain._grainFactory.GetInventoryGrain(item.OwnerId);

        await inventory.RemoveFurnitureAsync(item.ObjectId, ct);

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
        if (!await _securityModule.CanManipulateFurniAsync(ctx))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        if (
            !await _furniModule.ValidateWallItemPlacementAsync(
                ctx,
                itemId,
                x,
                y,
                z,
                wallOffset,
                rot
            )
        )
            throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

        if (!await _furniModule.MoveWallItemByIdAsync(ctx, itemId, x, y, z, wallOffset, rot, ct))
            return false;

        return true;
    }

    public async Task<bool> RemoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    )
    {
        var pickupType = await _securityModule.GetFurniPickupTypeAsync(ctx);

        if (pickupType == FurniturePickupType.None)
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        PlayerId pickerId = -1;

        if (pickupType is not FurniturePickupType.SendToOwner)
            pickerId = ctx.PlayerId;

        var wallItem = await _furniModule.RemoveWallItemByIdAsync(ctx, itemId, ct, pickerId);

        if (wallItem is null)
            return false;

        var inventory = _roomGrain._grainFactory.GetInventoryGrain(wallItem.OwnerId);

        await inventory.AddFurnitureFromRoomItemSnapshotAsync(wallItem, ct);

        return true;
    }

    public async Task<bool> UseWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_state.WallItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        var usagePolicy = item.Logic.GetUsagePolicy();

        if (
            !await _securityModule.CanUseFurniAsync(ctx, usagePolicy)
            || !await _furniModule.UseWallItemByIdAsync(ctx, itemId, ct, param)
        )
            return false;

        return true;
    }

    public Task<bool> ClickWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    ) => _furniModule.ClickWallItemByIdAsync(ctx, itemId, ct, param);
}
