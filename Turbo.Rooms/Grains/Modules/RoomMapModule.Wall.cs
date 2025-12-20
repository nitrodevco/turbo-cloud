using System.Threading;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomMapModule
{
    public bool AddWallItem(IRoomWallItem item, bool flush = true)
    {
        if (flush)
            _ = _roomGrain.SendComposerToRoomAsync(item.GetAddComposer(), CancellationToken.None);

        return true;
    }

    public bool PlaceWallItem(
        IRoomWallItem item,
        int x,
        int y,
        double z,
        Rotation rot,
        int wallOffset,
        bool flush = true
    )
    {
        item.SetPosition(x, y, z);
        item.SetRotation(rot);
        item.SetWallOffset(wallOffset);

        return AddWallItem(item, flush);
    }

    public bool MoveWallItemItem(
        IRoomWallItem item,
        int x,
        int y,
        double z,
        Rotation rot,
        int wallOffset,
        bool flush = true
    )
    {
        RemoveWallItem(item, -1, false);

        item.SetPosition(x, y, z);
        item.SetRotation(rot);
        item.SetWallOffset(wallOffset);

        AddWallItem(item, false);

        if (flush)
            _ = _roomGrain.SendComposerToRoomAsync(
                item.GetUpdateComposer(),
                CancellationToken.None
            );

        return true;
    }

    public bool RemoveWallItem(IRoomWallItem item, PlayerId pickerId, bool flush = true)
    {
        if (flush)
            _ = _roomGrain.SendComposerToRoomAsync(
                item.GetRemoveComposer(pickerId),
                CancellationToken.None
            );

        return true;
    }
}
