using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomActionModule
{
    public Task<bool> AddFloorItemAsync(IRoomFloorItem item, CancellationToken ct)
    {
        return _furniModule.AddFloorItemAsync(item, ct);
    }

    public async Task<bool> PlaceFloorItemAsync(
        ActionContext ctx,
        FurnitureFloorItemSnapshot item,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    )
    {
        if (!await CanManipulateFurniAsync(ctx))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        if (!_furniModule.ValidateNewFloorItemPlacement(ctx, item, newX, newY, newRotation))
            throw new TurboException(TurboErrorCodeEnum.InvalidFloorItemPlacement);

        if (!await _furniModule.PlaceFloorItemAsync(ctx, item, newX, newY, newRotation, ct))
            return false;

        // TODO add player name to owner names cache

        return true;
    }

    public async Task<bool> MoveFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    )
    {
        if (!await CanManipulateFurniAsync(ctx))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        if (!_furniModule.ValidateFloorItemPlacement(ctx, objectId, newX, newY, newRotation))
            throw new TurboException(TurboErrorCodeEnum.InvalidFloorItemPlacement);

        if (!await _furniModule.MoveFloorItemByIdAsync(ctx, objectId, newX, newY, newRotation, ct))
            return false;

        return true;
    }

    public Task<bool> RemoveFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct
    ) => _furniModule.RemoveFloorItemByIdAsync(ctx, objectId, ct);

    public async Task<bool> UseFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_state.FloorItemsById.TryGetValue(objectId.Value, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        var controllerLevel = await GetControllerLevelAsync(ctx);
        var usagePolicy = item.Logic.GetUsagePolicy();

        if (usagePolicy == FurnitureUsageType.Nobody)
            return false;

        if (usagePolicy == FurnitureUsageType.Controller)
        {
            if (controllerLevel < RoomControllerType.Rights)
                return false;
        }

        await item.Logic.OnUseAsync(ctx, param, ct);

        return true;
    }

    public Task<bool> ClickFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct,
        int param = -1
    ) => _furniModule.ClickFloorItemByIdAsync(ctx, objectId, ct, param);
}
