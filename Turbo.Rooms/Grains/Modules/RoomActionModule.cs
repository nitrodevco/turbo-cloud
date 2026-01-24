using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomActionModule(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;

    public Task<bool> AddItemAsync(IRoomItem item, CancellationToken ct) =>
        _roomGrain.ObjectModule.AttatchObjectAsync(item, ct);

    public async Task<bool> RemoveItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    )
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        var pickupType = await _roomGrain.SecurityModule.GetFurniPickupTypeAsync(ctx);

        if (pickupType == FurniturePickupType.None)
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        PlayerId pickerId = item.OwnerId;

        if (pickupType is FurniturePickupType.SendToCtx)
            pickerId = ctx.PlayerId;

        item.SetOwnerId(pickerId);

        await _roomGrain.ObjectModule.RemoveObjectAsync(ctx, item, ct, pickerId);

        var snapshot = item.GetSnapshot();

        var inventory = _roomGrain._grainFactory.GetInventoryGrain(snapshot.OwnerId);

        await inventory.AddFurnitureFromRoomItemSnapshotAsync(snapshot, ct);

        return true;
    }

    public async Task<bool> UseItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        var usagePolicy = item.Logic.GetUsagePolicy();

        if (
            !await _roomGrain.SecurityModule.CanUseFurniAsync(ctx, usagePolicy)
            || !await _roomGrain.FurniModule.UseItemByIdAsync(ctx, itemId, ct, param)
        )
            return false;

        return true;
    }

    public Task<bool> ClickItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    ) => _roomGrain.FurniModule.ClickItemByIdAsync(ctx, itemId, ct, param);
}
