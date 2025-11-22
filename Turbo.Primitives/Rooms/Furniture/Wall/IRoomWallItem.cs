using Turbo.Primitives.Rooms.Furniture.Logic;

namespace Turbo.Primitives.Rooms.Furniture.Wall;

public interface IRoomWallItem : IRoomItem
{
    public IFurnitureWallLogic Logic { get; }

    public void SetLogic(IFurnitureWallLogic logic);
}
