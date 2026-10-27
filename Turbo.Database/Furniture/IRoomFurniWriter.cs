using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Database.Context;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Database.Furniture;

/// <summary>A furni to make straight into a room: its definition, the extra data it starts with and where it stands.</summary>
public readonly record struct NewRoomFurni(
    int DefinitionId,
    string? ExtraDataJson,
    RoomPosition Position
);

/// <summary>
/// Makes furni straight into a room inside the caller's unit of work: a plugin writing a whole
/// room (its row, its floor plan, its furni) commits all of it or none. The furni are written as
/// every furni the hotel makes is, through its one writer of who holds a furni and where, with a
/// movement each under the caller's <see cref="MovementCause"/> (<c>docs/movements.md</c>).
/// Take it by constructor; the room must not be loaded yet (a loaded room does not read rows
/// written behind it).
/// </summary>
public interface IRoomFurniWriter
{
    /// <summary>
    /// One row per furni, owned by <paramref name="owner"/> and standing in
    /// <paramref name="room"/> where it says. The rows are saved, so their ids exist, when it
    /// answers, in the order of <paramref name="furni"/>; the caller commits.
    /// </summary>
    Task<IReadOnlyList<int>> CreateInRoomAsync(
        TurboDbContext db,
        RoomId room,
        PlayerId owner,
        IReadOnlyList<NewRoomFurni> furni,
        MovementCause cause,
        CancellationToken ct
    );
}
