using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Object.Furniture.Floor;

public interface IRoomFloorItem<TSelf, out TLogic, out TContext>
    : IRoomItem<TSelf, TLogic, TContext>
    where TSelf : IRoomFloorItem<TSelf, TLogic, TContext>
    where TContext : IRoomFloorItemContext<TSelf, TLogic, TContext>
    where TLogic : IFurnitureFloorLogic<TSelf, TLogic, TContext>
{
    new TLogic Logic { get; }
}

public interface IRoomFloorItem
    : IRoomItem<IRoomFloorItem, IFurnitureFloorLogic, IRoomFloorItemContext>
{
    public void SetRotation(Rotation rotation);
    public double GetStackHeight();
    new RoomFloorItemSnapshot GetSnapshot();
}
