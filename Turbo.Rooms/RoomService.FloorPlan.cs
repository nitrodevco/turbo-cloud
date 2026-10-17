using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Orleans;
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

            var evicted = await room.SaveFloorPlanAsync(ctx, modelData, properties, ct)
                .ConfigureAwait(false);

            if (evicted is null)
                return;

            // Habbo reloads the room for everyone in it: each client is told the room closed,
            // with no notification, and about two seconds later is forwarded back into the same
            // room, which comes up afresh on the new plan (official client capture, 2026-10-17,
            // six saves with a second player in the room: UserRemove + CloseConnection ~100 ms
            // after UpdateFloorProperties, RoomForward to the same room ~2.0 s later, both players
            // then on the door tile facing the new direction, object ids restarted).
            foreach (var playerId in evicted.Value)
            {
                await _grainFactory
                    .GetPlayerPresenceGrain(playerId)
                    .OnRemovedFromRoomAsync(ctx.RoomId, false, ct)
                    .ConfigureAwait(false);
            }

            if (_roomConfig.FloorPlanSaveReturnDelayMs > 0)
                await Task.Delay(
                        TimeSpan.FromMilliseconds(_roomConfig.FloorPlanSaveReturnDelayMs),
                        _time,
                        ct
                    )
                    .ConfigureAwait(false);

            foreach (var playerId in evicted.Value)
            {
                await _grainFactory
                    .ForwardPlayerToRoomAsync(playerId, ctx.RoomId, ct)
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
