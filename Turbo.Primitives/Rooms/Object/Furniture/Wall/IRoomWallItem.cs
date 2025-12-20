using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Object.Furniture.Wall;

public interface IRoomWallItem : IRoomItem
{
    public int WallOffset { get; }
    public IFurnitureWallLogic Logic { get; }

    public void SetPosition(int x, int y, double z);
    public void SetWallOffset(int wallOffset);
    public void SetRotation(Rotation rot);
    public void SetLogic(IFurnitureWallLogic logic);
    public RoomWallItemSnapshot GetSnapshot();
    public IComposer GetAddComposer();
    public IComposer GetUpdateComposer();
    public IComposer GetRefreshStuffDataComposer();
    public IComposer GetRemoveComposer(PlayerId pickerId);
    public string ConvertWallPositionToString();
}
