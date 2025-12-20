using System.Text.Json;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Object.Furniture.Wall;

internal sealed class RoomWallItem : RoomItem, IRoomWallItem
{
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
        new ItemDataUpdateMessageComposer
        {
            ObjectId = ObjectId,
            State = Logic.StuffData.GetLegacyString(),
        };

    public IComposer GetRemoveComposer(PlayerId pickerId) =>
        new ItemRemoveMessageComposer { ObjectId = ObjectId, PickerId = (int)pickerId };

    public string ConvertWallPositionToString() =>
        $":w={X},{Y} l={WallOffset},{Z} {(Rotation == Rotation.South ? "l" : "r")}";

    private RoomWallItemSnapshot BuildSnapshot() =>
        new()
        {
            ObjectId = ObjectId,
            OwnerId = OwnerId,
            OwnerName = OwnerName,
            DefinitionId = Definition.Id,
            SpriteId = Definition.SpriteId,
            X = X,
            Y = Y,
            Z = Z,
            Rotation = Rotation,
            StuffData = Logic.StuffData.GetSnapshot(),
            StuffDataJson = JsonSerializer.Serialize(Logic.StuffData, Logic.StuffData.GetType()),
            UsagePolicy = Logic.GetUsagePolicy(),
            WallOffset = WallOffset,
            WallPosition = ConvertWallPositionToString(),
        };
}
