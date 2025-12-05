using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;

namespace Turbo.Rooms;

internal sealed partial class RoomService
{
    public async Task MoveFloorItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId.Value <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId.Value);

        if (
            await roomGrain
                .MoveFloorItemByIdAsync(ctx, itemId, newX, newY, newRotation, ct)
                .ConfigureAwait(false)
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
