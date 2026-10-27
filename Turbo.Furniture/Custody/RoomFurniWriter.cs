using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Database.Context;
using Turbo.Database.Furniture;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Furniture.Custody;

/// <summary>The plugins' way to make furni into a room (<see cref="IRoomFurniWriter"/>): the custody's own transition.</summary>
public sealed class RoomFurniWriter(IFurnitureCustody custody) : IRoomFurniWriter
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
