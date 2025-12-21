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
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomActionModule
{
    public Task<bool> AddFloorItemAsync(IRoomFloorItem item, CancellationToken ct) =>
        _furniModule.AddFloorItemAsync(item, ct);

    public async Task<bool> PlaceFloorItemAsync(
        ActionContext ctx,
        FurnitureItemSnapshot snapshot,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (!await _securityModule.CanPlaceFurniAsync(ctx))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToPlaceFurni);

        var item = _roomGrain._itemsLoader.CreateFromFurnitureItemSnapshot(snapshot);

        if (item is not IRoomFloorItem floorItem)
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (!_furniModule.ValidateNewFloorItemPlacement(ctx, floorItem, x, y, rot))
            throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

        if (!await _furniModule.PlaceFloorItemAsync(ctx, floorItem, x, y, rot, ct))
            return false;

        var inventory = _roomGrain._grainFactory.GetInventoryGrain(item.OwnerId);

        await inventory.RemoveFurnitureAsync(item.ObjectId, ct);

        return true;
    }

    public async Task<bool> MoveFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (!await _securityModule.CanManipulateFurniAsync(ctx))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        if (!_furniModule.ValidateFloorItemPlacement(ctx, itemId, x, y, rot))
            throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

        if (!await _furniModule.MoveFloorItemByIdAsync(ctx, itemId, x, y, rot, ct))
            return false;

        return true;
    }

    public async Task<bool> RemoveFloorItemByIdAsync(
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

        var floorItem = await _furniModule.RemoveFloorItemByIdAsync(ctx, itemId, ct, pickerId);

        if (floorItem is null)
            return false;

        var inventory = _roomGrain._grainFactory.GetInventoryGrain(floorItem.OwnerId);

        await inventory.AddFurnitureFromRoomItemSnapshotAsync(floorItem, ct);

        return true;
    }

    public async Task<bool> UseFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_state.FloorItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        var usagePolicy = item.Logic.GetUsagePolicy();

        if (
            !await _securityModule.CanUseFurniAsync(ctx, usagePolicy)
            || !await _furniModule.UseFloorItemByIdAsync(ctx, itemId, ct, param)
        )
            return false;

        return true;
    }

    public Task<bool> ClickFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    ) => _furniModule.ClickFloorItemByIdAsync(ctx, itemId, ct, param);
}
