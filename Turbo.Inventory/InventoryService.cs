using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;

namespace Turbo.Inventory;

public sealed class InventoryService(ILogger<IInventoryService> logger, IGrainFactory grainFactory)
    : IInventoryService
{
    private readonly ILogger<IInventoryService> _logger = logger;
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async Task PlaceFloorItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (
            ctx is null
            || ctx.Origin != ActionOrigin.Player
            || ctx.PlayerId <= 0
            || ctx.RoomId.Value <= 0
        )
            return;

        try
        {
            var inventoryGrain = _grainFactory.GetGrain<IInventoryGrain>(ctx.PlayerId);

            var snapshot = await inventoryGrain
                .GetItemSnapshotAsync(itemId, ct)
                .ConfigureAwait(false);

            if (snapshot is null)
                return;

            var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId.Value);

            if (snapshot is not FurnitureFloorItemSnapshot floorSnapshot)
                return;
            if (
                !await roomGrain
                    .PlaceFloorItemAsync(ctx, floorSnapshot, x, y, rot, ct)
                    .ConfigureAwait(false)
            )
            {
                // failed
                return;
            }

            await inventoryGrain.RemoveItemAsync(itemId, ct).ConfigureAwait(false);
        }
        catch (Exception) { }
    }

    public async Task PlaceWallItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        int x,
        int y,
        double z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (
            ctx is null
            || ctx.Origin != ActionOrigin.Player
            || ctx.PlayerId <= 0
            || ctx.RoomId.Value <= 0
        )
            return;

        try
        {
            var inventoryGrain = _grainFactory.GetGrain<IInventoryGrain>(ctx.PlayerId);

            var snapshot = await inventoryGrain
                .GetItemSnapshotAsync(itemId, ct)
                .ConfigureAwait(false);

            if (snapshot is null)
                return;

            var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId.Value);

            if (snapshot is not FurnitureWallItemSnapshot wallSnapshot)
                return;

            if (
                !await roomGrain
                    .PlaceWallItemAsync(ctx, wallSnapshot, x, y, z, wallOffset, rot, ct)
                    .ConfigureAwait(false)
            )
            {
                // failed
                return;
            }

            await inventoryGrain.RemoveItemAsync(itemId, ct).ConfigureAwait(false);
        }
        catch (Exception) { }
    }
}
