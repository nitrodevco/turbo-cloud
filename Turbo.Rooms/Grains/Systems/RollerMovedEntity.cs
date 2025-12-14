using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Grains.Systems;

internal sealed record RollerMovedEntity
{
    public required RoomObjectId ObjectId { get; init; }
    public required IRoomObject RoomObject { get; init; }
    public required double FromZ { get; init; }
    public required double ToZ { get; init; }
}
