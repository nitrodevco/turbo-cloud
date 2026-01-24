using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomMapModule
{
    private bool AddWallItem(IRoomWallItem item)
    {
        return true;
    }

    public bool PlaceWallItem(
        IRoomWallItem item,
        int x,
        int y,
        Altitude z,
        Rotation rot,
        int wallOffset
    )
    {
        item.SetPosition(x, y);
        item.SetPositionZ(z);
        item.SetRotation(rot);
        item.SetWallOffset(wallOffset);

        return AddWallItem(item);
    }

    public bool MoveWallItem(
        IRoomWallItem item,
        int x,
        int y,
        Altitude z,
        Rotation rot,
        int wallOffset
    )
    {
        RemoveWallItem(item);

        item.SetPosition(x, y);
        item.SetPositionZ(z);
        item.SetRotation(rot);
        item.SetWallOffset(wallOffset);

        AddWallItem(item);

        return true;
    }

    public bool RemoveWallItem(IRoomWallItem item)
    {
        return true;
    }
}
