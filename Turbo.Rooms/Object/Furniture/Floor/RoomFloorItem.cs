using System.Collections.Immutable;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Object.Furniture.Floor;

public sealed class RoomFloorItem
    : RoomItem<IRoomFloorItem, IFurnitureFloorLogic, IRoomFloorItemContext>,
        IRoomFloorItem
{
    public new RoomFloorItemSnapshot GetSnapshot() => (RoomFloorItemSnapshot)base.GetSnapshot();

    /// <summary>
    /// A temporary furni arrives as Habbo sends one: the room objects message holding it alone,
    /// with its owner's name, rather than ObjectAdd (official client capture, WIN63-202610131318,
    /// Place Temporary Furni: Objects with one floor item and owners {room owner}, then a height
    /// map update). The client adds the objects of either message the same way
    /// (<c>RoomMessageHandler.as:670</c>, <c>:690</c>); only ObjectAdd also tells the room
    /// competition that the room changed (<c>quest/IncomingMessages.as:161</c>).
    /// </summary>
    public override IComposer GetAddComposer()
    {
        var snapshot = GetSnapshot();

        if (!IsTemporary)
            return new ObjectAddMessageComposer { FloorItem = snapshot };

        return new ObjectsMessageComposer
        {
            OwnerNames = ImmutableDictionary<PlayerId, string>.Empty.Add(
                snapshot.OwnerId,
                snapshot.OwnerName
            ),
            FloorItems = [snapshot],
        };
    }

    public override IComposer GetUpdateComposer() =>
        new ObjectUpdateMessageComposer { FloorItem = GetSnapshot() };

    public override IComposer GetRefreshStuffDataComposer()
    {
        if (Logic is FurnitureDiceLogic)
            return new DiceValueMessageComposer
            {
                FurniId = ObjectId,
                Value = Logic.StuffData.GetState(),
            };

        return new ObjectDataUpdateMessageComposer
        {
            ObjectId = ObjectId,
            StuffData = Logic.StuffData.GetSnapshot(),
        };
    }

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
            ByInvisibleFurniManager = false,
        };

    public override IComposer GetHideComposer() =>
        new ObjectRemoveMessageComposer
        {
            ObjectId = ObjectId,
            IsExpired = false,
            PickerId = PlayerId.Invalid,
            Delay = 0,
            ByInvisibleFurniManager = true,
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
            Extra = Logic.GetObjectExtra(),
        };
}
