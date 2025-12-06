using System;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Object.Furniture.Wall;

internal sealed class RoomWallItem : RoomItem, IRoomWallItem
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public double Z { get; private set; }
    public Rotation Rotation { get; private set; }
    public int WallOffset { get; private set; }
    public IFurnitureWallLogic Logic { get; private set; } = default!;

    private RoomWallItemSnapshot? _snapshot;

    public void SetPosition(int x, int y, double z)
    {
        if (X == x && Y == y && Z == z)
            return;

        X = x;
        Y = y;
        Z = z;

        MarkDirty();
    }

    public void SetWallOffset(int wallOffset)
    {
        if (WallOffset == wallOffset)
            return;

        WallOffset = wallOffset;

        MarkDirty();
    }

    public void SetRotation(Rotation rot)
    {
        if (Rotation == rot)
            return;

        Rotation = rot;

        MarkDirty();
    }

    public void SetLogic(IFurnitureWallLogic logic)
    {
        PendingStuffDataRaw = string.Empty;
        Logic = logic;
    }

    public RoomWallItemSnapshot GetSnapshot()
    {
        if (_dirty || _snapshot is null)
        {
            _snapshot = BuildSnapshot();
            _dirty = false;
        }

        return _snapshot;
    }

    public IComposer GetAddComposer() => new ItemAddMessageComposer { WallItem = GetSnapshot() };

    public IComposer GetUpdateComposer() =>
        new ItemUpdateMessageComposer { WallItem = GetSnapshot() };

    public IComposer GetRefreshStuffDataComposer() =>
        new ItemDataUpdateMessageComposer { ObjectId = ObjectId, State = GetSnapshot().StuffData };

    public IComposer GetRemoveComposer(long pickerId) =>
        new ItemRemoveMessageComposer { ObjectId = ObjectId, PickerId = (int)pickerId };

    public string ConvertWallPositionToString() =>
        $":w={X},{Y} l={WallOffset},{Math.Truncate(Z)} {(Rotation == Rotation.South ? "l" : "r")}";

    private RoomWallItemSnapshot BuildSnapshot() =>
        new()
        {
            ObjectId = ObjectId,
            OwnerId = OwnerId,
            OwnerName = OwnerName,
            SpriteId = Definition.SpriteId,
            X = X,
            Y = Y,
            Z = Z,
            WallOffset = WallOffset,
            Rotation = Rotation,
            WallPosition = ConvertWallPositionToString(),
            StuffData = Logic.StuffData,
            UsagePolicy = Definition.UsagePolicy,
        };
}
