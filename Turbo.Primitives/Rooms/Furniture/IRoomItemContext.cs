using Turbo.Primitives.Rooms.Furniture.StuffData;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Furniture;

public interface IRoomItemContext
{
    public long RoomId { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
}
