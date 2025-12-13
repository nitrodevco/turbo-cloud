using System;
using System.Text.Json;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Object.Furniture.Floor;

internal sealed class RoomFloorItem : RoomItem, IRoomFloorItem
{
    public IFurnitureFloorLogic Logic { get; private set; } = default!;

    private RoomFloorItemSnapshot? _snapshot;

    public double Height => Logic?.GetHeight() ?? Definition.StackHeight;

    public void SetPosition(int x, int y, double z)
    {
        z = Math.Round(z, 3);

        if (X == x && Y == y && Z == z)
            return;

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
            StuffData = Logic.StuffData.GetSnapshot(),
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
            DefinitionId = Definition.Id,
            SpriteId = Definition.SpriteId,
            X = X,
            Y = Y,
            Z = Z,
            Rotation = Rotation,
            StuffData = Logic.StuffData.GetSnapshot(),
            StuffDataJson = JsonSerializer.Serialize(Logic.StuffData, Logic.StuffData.GetType()),
            UsagePolicy = Definition.UsagePolicy,
            StackHeight = Definition.StackHeight,
        };
}
