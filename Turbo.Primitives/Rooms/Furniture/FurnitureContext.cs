using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Furniture;

public sealed class FurnitureContext
{
    public required RoomFloorItemSnapshot ItemSnapshot { get; init; }
    public required FurnitureDefinitionSnapshot DefinitionSnapshot { get; init; }
}
