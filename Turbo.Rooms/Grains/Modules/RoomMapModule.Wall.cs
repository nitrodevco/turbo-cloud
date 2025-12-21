using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomMapModule
{
    public bool AddWallItem(IRoomWallItem item)
    {
        return true;
    }

    public bool PlaceWallItem(
        IRoomWallItem item,
        int x,
        int y,
        double z,
        Rotation rot,
        int wallOffset
    )
    {
        item.SetPosition(x, y, z);
        item.SetRotation(rot);
        item.SetWallOffset(wallOffset);

        return AddWallItem(item);
    }

    public bool MoveWallItemItem(
        IRoomWallItem item,
        int x,
        int y,
        double z,
        Rotation rot,
        int wallOffset
    )
    {
        RemoveWallItem(item);

        item.SetPosition(x, y, z);
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
