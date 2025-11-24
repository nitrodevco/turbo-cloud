using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Contracts.Enums.Rooms;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Actor;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomActionModule
{
    public Task<bool> AddFloorItemAsync(IRoomFloorItem item, CancellationToken ct)
    {
        return _furniModule.AddFloorItemAsync(item, ct);
    }

    public async Task<bool> MoveFloorItemByIdAsync(
        ActorContext ctx,
        long itemId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    )
    {
        if (!await CanManipulateFurniAsync(ctx))
            return false;

        if (
            !await _furniModule.ValidateFloorItemPlacementAsync(
                ctx,
                itemId,
                newX,
                newY,
                newRotation
            )
        )
            return false;

        if (!await _furniModule.MoveFloorItemByIdAsync(ctx, itemId, newX, newY, newRotation, ct))
            return false;

        _ = _roomGrain.PublishRoomEventAsync(
            new FloorItemMovedEvent
            {
                RoomId = _roomGrain.GetPrimaryKeyLong(),
                CausedBy = ctx,
                ItemId = itemId,
            },
            ct
        );

        return true;
    }

    public Task<bool> RemoveFloorItemByIdAsync(
        ActorContext ctx,
        long itemId,
        CancellationToken ct
    ) => _furniModule.RemoveFloorItemByIdAsync(ctx, itemId, ct);

    public async Task<bool> UseFloorItemByIdAsync(
        ActorContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    )
    {
        if (!_state.FloorItemsById.TryGetValue(itemId, out var item))
            return false;

        var controllerLevel = await GetControllerLevelAsync(ctx);
        var usagePolicy = item.Logic.GetUsagePolicy();

        if (usagePolicy == FurniUsagePolicy.Nobody)
            return false;

        if (usagePolicy == FurniUsagePolicy.Controller)
        {
            if (controllerLevel < RoomControllerLevel.Rights)
                return false;
        }

        await item.Logic.OnUseAsync(ctx, param, ct);

        return true;
    }

    public Task<bool> ClickFloorItemByIdAsync(
        ActorContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    ) => _furniModule.ClickFloorItemByIdAsync(ctx, itemId, param, ct);
}
