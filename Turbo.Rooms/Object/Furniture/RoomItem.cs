using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Rooms.Object.Furniture;

namespace Turbo.Rooms.Object.Furniture;

internal abstract class RoomItem : RoomObject, IRoomItem
{
    public required long OwnerId { get; init; }
    public required string OwnerName { get; set; } = string.Empty;
    public required string PendingStuffDataRaw { get; set; } = string.Empty;
    public required FurnitureDefinitionSnapshot Definition { get; init; }

    public void SetOwnerName(string ownerName)
    {
        OwnerName = ownerName;
    }
}
