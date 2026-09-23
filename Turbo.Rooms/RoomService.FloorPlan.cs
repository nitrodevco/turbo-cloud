using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Rooms;

internal sealed partial class RoomService
{
    public async Task SaveFloorPlanAsync(
        ActionContext ctx,
        string modelData,
        FloorPlanPropertiesSnapshot? properties,
        CancellationToken ct
    )
    {
        if (ctx.Origin != ActionOrigin.Player || ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        try
        {
            var room = _grainFactory.GetRoomGrain(ctx.RoomId);

            var occupants = await room.SaveFloorPlanAsync(ctx, modelData, properties, ct)
                .ConfigureAwait(false);

            if (occupants is null)
                return;

            // The room is streamed again rather than patched: the tile arrays are a different
            // size, the furni may have moved and the avatars have all been put at the door, so
            // there is nothing left of what the client was told that still holds.
            foreach (var playerId in occupants.Value)
            {
                await EnterRoomAsync(
                        ActionContext.CreateForPlayer(playerId, ctx.RoomId),
                        _grainFactory.GetPlayerPresenceGrain(playerId),
                        room,
                        ctx.RoomId,
                        ct
                    )
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save the floor plan of room {RoomId} for player {PlayerId}",
                ctx.RoomId,
                ctx.PlayerId
            );
        }
    }
}
