using Turbo.Primitives.Furniture.Snapshots;

namespace Turbo.Primitives.Rooms.Object.Furniture;

public interface IRoomItem : IRoomObject
{
    public long OwnerId { get; }
    public string OwnerName { get; }
    public string PendingStuffDataRaw { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
    public void SetOwnerName(string ownerName);
}
