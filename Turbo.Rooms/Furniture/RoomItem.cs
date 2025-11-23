using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Rooms.Furniture;

internal abstract class RoomItem : IRoomItem
{
    public required long Id { get; init; }
    public required long OwnerId { get; init; }
    public required string OwnerName { get; set; } = string.Empty;
    public required string PendingStuffDataRaw { get; set; } = string.Empty;
    public required FurnitureDefinitionSnapshot Definition { get; init; }
}
