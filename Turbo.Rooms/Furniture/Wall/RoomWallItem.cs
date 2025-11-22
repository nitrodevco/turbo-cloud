using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.Wall;

namespace Turbo.Rooms.Furniture.Wall;

public sealed class RoomWallItem : RoomItem, IRoomWallItem
{
    public IFurnitureWallLogic Logic { get; private set; } = default!;

    public void SetLogic(IFurnitureWallLogic logic)
    {
        Logic = logic;

        //setup stuff data
    }
}
