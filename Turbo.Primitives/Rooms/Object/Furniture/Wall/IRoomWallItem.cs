using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Object.Furniture.Wall;

public interface IRoomWallItem : IRoomItem
{
    public int X { get; }
    public int Y { get; }
    public double Z { get; }
    public Rotation Rotation { get; }
    public int WallOffset { get; }
    public IFurnitureWallLogic Logic { get; }

    public void SetPosition(int x, int y, double z, int wallOffset, Rotation rot);
    public void SetLogic(IFurnitureWallLogic logic);
    public RoomWallItemSnapshot GetSnapshot();
    public IComposer GetAddComposer();
    public IComposer GetUpdateComposer();
    public IComposer GetRefreshStuffDataComposer();
    public IComposer GetRemoveComposer(long pickerId);
    public string ConvertWallPositionToString();
}
