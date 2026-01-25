using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Object.Furniture.Floor;

public sealed class RoomFloorItem
    : RoomItem<IRoomFloorItem, IFurnitureFloorLogic, IRoomFloorItemContext>,
        IRoomFloorItem
{
    public new RoomFloorItemSnapshot GetSnapshot() => (RoomFloorItemSnapshot)base.GetSnapshot();

    public override IComposer GetAddComposer() =>
        new ObjectAddMessageComposer { FloorItem = GetSnapshot() };

    public override IComposer GetUpdateComposer() =>
        new ObjectUpdateMessageComposer { FloorItem = GetSnapshot() };

    public override IComposer GetRefreshStuffDataComposer() =>
        new ObjectDataUpdateMessageComposer
        {
            ObjectId = ObjectId,
            StuffData = Logic.StuffData.GetSnapshot(),
        };

    public override IComposer GetRemoveComposer(
        PlayerId pickerId,
        bool isExpired = false,
        int delay = 0
    ) =>
        new ObjectRemoveMessageComposer
        {
            ObjectId = ObjectId,
            IsExpired = isExpired,
            PickerId = pickerId,
            Delay = delay,
        };

    protected override RoomItemSnapshot BuildSnapshot() =>
        new RoomFloorItemSnapshot()
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
            StackHeight = GetStackHeight(),
        };
}
