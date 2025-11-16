using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.StuffData;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Rooms.Furniture;

public class RoomItem<TLogic> : IRoomItem<TLogic>
    where TLogic : IFurnitureLogic
{
    public required long Id { get; init; }
    public required long OwnerId { get; init; }
    public required FurnitureDefinitionSnapshot Definition { get; init; }
    public required TLogic Logic { get; init; }
    public required IStuffData StuffData { get; init; }
}
