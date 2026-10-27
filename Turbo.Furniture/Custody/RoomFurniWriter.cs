using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Furniture;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Furniture.Custody;

/// <summary>
/// The plugins' way to make furni into a room (<see cref="IRoomFurniWriter"/>): the custody's own
/// transition. A loaded room is refused: it holds its furni in memory and never reads rows written
/// behind it, so they would stay out of sight until it next loads.
/// </summary>
public sealed class RoomFurniWriter(IFurnitureCustody custody, IGrainFactory grainFactory)
    : IRoomFurniWriter
{
    public async Task<IReadOnlyList<int>> CreateInRoomAsync(
        TurboDbContext db,
        RoomId room,
        PlayerId owner,
        IReadOnlyList<NewRoomFurni> furni,
        MovementCause cause,
        CancellationToken ct
    )
    {
        var active = await grainFactory
            .GetRoomDirectoryGrain()
            .GetActiveRoomIdsAsync(ct)
            .ConfigureAwait(false);

        if (active.Contains(room))
            throw new InvalidOperationException(
                $"Room {room.Value} is loaded: furni are made straight into a room only before it loads."
            );

        var made = await custody
            .CreateInRoomAsync(
                db,
                room,
                owner,
                [
                    .. furni.Select(x => new FurniToPlace(
                        new FurniToMake(x.DefinitionId, x.ExtraDataJson),
                        x.Position
                    )),
                ],
                cause,
                ct
            )
            .ConfigureAwait(false);

        return [.. made.Select(x => x.Id)];
    }
}
