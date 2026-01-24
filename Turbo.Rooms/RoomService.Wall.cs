using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms;

internal sealed partial class RoomService
{
    public async Task PlaceWallItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Altitude z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (ctx.Origin != ActionOrigin.Player || ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        try
        {
            var inventoryGrain = _grainFactory.GetInventoryGrain(ctx.PlayerId);

            var snapshot = await inventoryGrain
                .GetItemSnapshotAsync(itemId, ct)
                .ConfigureAwait(false);

            if (snapshot is null || snapshot.Definition.ProductType != ProductType.Wall)
                return;

            var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

            if (
                !await roomGrain
                    .PlaceWallItemAsync(ctx, snapshot, x, y, z, wallOffset, rot, ct)
                    .ConfigureAwait(false)
            )
            {
                // failed
                return;
            }
        }
        catch (Exception) { }
    }

    public async Task MoveWallItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Altitude z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

        if (
            await roomGrain
                .MoveWallItemByIdAsync(ctx, itemId, x, y, z, wallOffset, rot, ct)
                .ConfigureAwait(false)
        )
            return;

        var item = await roomGrain.GetWallItemSnapshotByIdAsync(itemId, ct).ConfigureAwait(false);

        if (item is null)
            return;

        var session = _sessionGateway.GetSession(ctx.SessionKey);

        if (session is not null)
            await session
                .SendComposerAsync(new ItemUpdateMessageComposer { WallItem = item }, ct)
                .ConfigureAwait(false);
    }
}
