using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;

namespace Turbo.Rooms;

internal sealed partial class RoomService
{
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

        var inventoryGrain = _grainFactory.GetGrain<IInventoryGrain>(ctx.PlayerId);

        var snapshot = await inventoryGrain
            .GetFloorItemSnapshotAsync(itemId, ct)
            .ConfigureAwait(false);

        if (snapshot is null)
            return;

        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId.Value);

        await roomGrain.PlaceFloorItemAsync(ctx, snapshot, x, y, rot, ct).ConfigureAwait(false);
    }

    public async Task MoveFloorItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId.Value <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId.Value);

        if (
            await roomGrain.MoveFloorItemByIdAsync(ctx, itemId, x, y, rot, ct).ConfigureAwait(false)
        )
            return;

        var item = await roomGrain.GetFloorItemSnapshotByIdAsync(itemId, ct).ConfigureAwait(false);

        if (item is null)
            return;

        var session = _sessionGateway.GetSession(ctx.SessionKey);

        if (session is not null)
            await session
                .SendComposerAsync(new ObjectUpdateMessageComposer { FloorItem = item }, ct)
                .ConfigureAwait(false);
    }

    public async Task UseFloorItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId.Value <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId.Value);

        await roomGrain.UseFloorItemByIdAsync(ctx, itemId, ct, param).ConfigureAwait(false);
    }

    public async Task ClickFloorItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId.Value <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId.Value);

        await roomGrain.ClickFloorItemByIdAsync(ctx, itemId, ct, param).ConfigureAwait(false);
    }
}
