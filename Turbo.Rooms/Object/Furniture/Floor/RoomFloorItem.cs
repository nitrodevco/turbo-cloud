using System.Text.Json;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.StuffData;

namespace Turbo.Rooms.Object.Furniture.Floor;

internal sealed class RoomFloorItem : RoomItem, IRoomFloorItem
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public double Z { get; private set; }
    public Rotation Rotation { get; private set; }
    public IFurnitureFloorLogic Logic { get; private set; } = default!;

    private RoomFloorItemSnapshot? _snapshot;

    public void SetPosition(int x, int y, double z)
    {
        X = x;
        Y = y;
        Z = z;

        MarkDirty();
    }

    public void SetRotation(Rotation rotation)
    {
        Rotation = rotation;

        MarkDirty();
    }

    public void SetLogic(IFurnitureFloorLogic logic)
    {
        PendingStuffDataRaw = string.Empty;
        Logic = logic;
    }

    public RoomFloorItemSnapshot GetSnapshot()
    {
        if (_dirty || _snapshot is null)
        {
            _snapshot = BuildSnapshot();
            _dirty = false;
        }

        return _snapshot;
    }

    public IComposer GetAddComposer() => new ObjectAddMessageComposer { FloorItem = GetSnapshot() };

    public IComposer GetUpdateComposer() =>
        new ObjectUpdateMessageComposer { FloorItem = GetSnapshot() };

    public IComposer GetRefreshStuffDataComposer() =>
        new ObjectDataUpdateMessageComposer
        {
            ObjectId = ObjectId,
            StuffData = GetSnapshot().StuffData,
        };

    public IComposer GetRemoveComposer(long pickerId, bool isExpired = false, int delay = 0) =>
        new ObjectRemoveMessageComposer
        {
            ObjectId = ObjectId,
            IsExpired = isExpired,
            PickerId = pickerId,
            Delay = delay,
        };

    private RoomFloorItemSnapshot BuildSnapshot() =>
        new()
        {
            ObjectId = ObjectId,
            OwnerId = OwnerId,
            OwnerName = OwnerName,
            SpriteId = Definition.SpriteId,
            X = X,
            Y = Y,
            Z = Z,
            Rotation = Rotation,
            StackHeight = Definition.StackHeight,
            StuffData = StuffDataSnapshot.FromStuffData(Logic.StuffData),
            StuffDataJson = JsonSerializer.Serialize(Logic.StuffData, Logic.StuffData.GetType()),
            UsagePolicy = Definition.UsagePolicy,
        };
}
