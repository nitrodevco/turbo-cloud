using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomActionModule
{
    /// <summary>
    /// Takes an inventory furni into this room in the database, before the room shows it. When
    /// the database no longer has it in its owner's inventory (traded, deposited in a chest,
    /// placed in another room a moment ago) the placement is refused, and the inventory lets go
    /// of the stale entry so the client stops offering it.
    /// </summary>
    private async Task<bool> ClaimForRoomAsync(
        IRoomItem item,
        int x,
        int y,
        Rotation rot,
        double? z,
        int? wallOffset,
        CancellationToken ct
    )
    {
        if (
            await FurnitureLocations.ClaimForRoomAsync(
                _roomGrain._dbCtxFactory,
                item.ObjectId.Value,
                item.OwnerId.Value,
                _roomGrain.RoomId.Value,
                x,
                y,
                rot,
                z,
                wallOffset,
                ct
            )
        )
            return true;

        await _roomGrain
            ._grainFactory.GetInventoryGrain(item.OwnerId)
            .ReleaseFurnitureAsync([item.ObjectId], ct);

        return false;
    }

    /// <summary>
    /// Runs the room's own placement of a claimed furni. A refusal (the spot is taken, a limit
    /// is reached) or a fault hands the furni back to its owner's inventory in the database.
    /// </summary>
    private async Task<bool> PlaceClaimedAsync(
        IRoomItem item,
        Func<Task<bool>> placement,
        CancellationToken ct
    )
    {
        bool placed;

        try
        {
            placed = await placement();
        }
        catch
        {
            await FurnitureLocations.UnclaimAsync(
                _roomGrain._dbCtxFactory,
                item.ObjectId.Value,
                _roomGrain.RoomId.Value,
                CancellationToken.None
            );

            throw;
        }

        if (!placed)
            await FurnitureLocations.UnclaimAsync(
                _roomGrain._dbCtxFactory,
                item.ObjectId.Value,
                _roomGrain.RoomId.Value,
                ct
            );

        return placed;
    }
}
