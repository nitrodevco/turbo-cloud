using Turbo.Primitives.Rooms.Furniture.StuffData;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Furniture;

public interface IRoomItem
{
    public long Id { get; }
    public long OwnerId { get; }
    public string OwnerName { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
}
