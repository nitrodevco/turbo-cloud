using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Rooms.Furniture.Logic;

namespace Turbo.Primitives.Rooms.Furniture.Floor;

public interface IRoomFloorItem : IRoomItem
{
    public int X { get; }
    public int Y { get; }
    public double Z { get; }
    public Rotation Rotation { get; }
    public IFurnitureFloorLogic Logic { get; }
    public void SetPosition(int x, int y, double z);
    public void SetRotation(Rotation rotation);
    public void SetLogic(IFurnitureFloorLogic logic);
}
