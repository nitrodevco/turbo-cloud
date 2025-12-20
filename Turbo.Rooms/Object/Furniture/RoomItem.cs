using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object.Furniture;

namespace Turbo.Rooms.Object.Furniture;

internal abstract class RoomItem : RoomObject, IRoomItem
{
    public required PlayerId OwnerId { get; set; }
    public required string OwnerName { get; set; } = string.Empty;
    public required string PendingStuffDataRaw { get; set; } = string.Empty;
    public required FurnitureDefinitionSnapshot Definition { get; init; }

    public void SetOwnerId(PlayerId ownerId)
    {
        OwnerId = ownerId;

        MarkDirty();
    }

    public void SetOwnerName(string ownerName)
    {
        OwnerName = ownerName;
    }
}
