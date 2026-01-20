using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Object.Furniture.Wall;

public class RoomWallItem
    : RoomItem<IRoomWallItem, IFurnitureWallLogic, IRoomWallItemContext>,
        IRoomWallItem
{
    public int WallOffset { get; private set; }

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

    public new RoomWallItemSnapshot GetSnapshot() => (RoomWallItemSnapshot)base.GetSnapshot();

    public override IComposer GetAddComposer() =>
        new ItemAddMessageComposer { WallItem = GetSnapshot() };

    public override IComposer GetUpdateComposer() =>
        new ItemUpdateMessageComposer { WallItem = GetSnapshot() };

    public override IComposer GetRefreshStuffDataComposer() =>
        new ItemDataUpdateMessageComposer
        {
            ObjectId = ObjectId,
            State = Logic.StuffData.GetLegacyString(),
        };

    public override IComposer GetRemoveComposer(
        PlayerId pickerId,
        bool isExpired = false,
        int delay = 0
    ) => new ItemRemoveMessageComposer { ObjectId = ObjectId, PickerId = pickerId };

    public string ConvertWallPositionToString() =>
        $":w={X},{Y} l={WallOffset},{Z} {(Rotation == Rotation.South ? "l" : "r")}";

    protected override RoomItemSnapshot BuildSnapshot() =>
        new RoomWallItemSnapshot()
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
            ExtraData = ExtraData.GetJsonString(),
            UsagePolicy = Logic.GetUsagePolicy(),
            WallOffset = WallOffset,
            WallPosition = ConvertWallPositionToString(),
        };
}
