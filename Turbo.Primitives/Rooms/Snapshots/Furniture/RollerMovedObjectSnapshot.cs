using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Snapshots.Furniture;

[GenerateSerializer, Immutable]
public sealed record RollerMovedObjectSnapshot
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required IRoomObject RoomObject { get; init; }

    [Id(2)]
    public required Altitude FromZ { get; init; }

    [Id(3)]
    public required Altitude ToZ { get; init; }
}
