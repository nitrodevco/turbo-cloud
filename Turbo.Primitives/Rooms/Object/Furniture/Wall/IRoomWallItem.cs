using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Object.Furniture.Wall;

public interface IRoomWallItem<TSelf, out TLogic, out TContext> : IRoomItem<TSelf, TLogic, TContext>
    where TSelf : IRoomWallItem<TSelf, TLogic, TContext>
    where TContext : IRoomWallItemContext<TSelf, TLogic, TContext>
    where TLogic : IFurnitureWallLogic<TSelf, TLogic, TContext>
{
    new TLogic Logic { get; }
}

public interface IRoomWallItem : IRoomItem<IRoomWallItem, IFurnitureWallLogic, IRoomWallItemContext>
{
    public int WallOffset { get; }

    public void SetWallOffset(int wallOffset);
    new RoomWallItemSnapshot GetSnapshot();
    public string ConvertWallPositionToString();
}
