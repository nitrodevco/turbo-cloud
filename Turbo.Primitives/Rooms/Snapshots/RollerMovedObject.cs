using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Snapshots;

public sealed record RollerMovedObject
{
    public required RoomObjectId ObjectId { get; init; }
    public required IRoomObject RoomObject { get; init; }
    public required Altitude FromZ { get; init; }
    public required Altitude ToZ { get; init; }
}
