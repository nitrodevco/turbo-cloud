using Turbo.Primitives.Rooms.Object.Furniture.Wall;

namespace Turbo.Primitives.Rooms.Object.Logic.Furniture;

public interface IFurnitureWallLogic : IFurnitureLogic
{
    public string StuffData { get; }
    public IRoomWallItemContext Context { get; }
}
