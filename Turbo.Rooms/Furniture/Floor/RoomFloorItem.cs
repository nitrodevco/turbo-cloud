using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Furniture.Logic;

namespace Turbo.Rooms.Furniture.Floor;

public sealed class RoomFloorItem : RoomItem, IRoomFloorItem
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public double Z { get; private set; }
    public Rotation Rotation { get; private set; }
    public IFurnitureFloorLogic Logic { get; private set; } = default!;

    public void SetPosition(int x, int y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public void SetRotation(Rotation rotation)
    {
        Rotation = rotation;
    }

    public void SetLogic(IFurnitureFloorLogic logic)
    {
        Logic = logic;

        logic.SetupStuffDataFromJson(GetStuffDataRaw());
    }
}
