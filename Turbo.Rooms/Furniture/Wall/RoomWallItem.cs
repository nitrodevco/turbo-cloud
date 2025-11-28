using System;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Furniture.Wall;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Furniture.Wall;

internal sealed class RoomWallItem : RoomItem, IRoomWallItem
{
    public string WallLocation { get; private set; } = string.Empty;
    public IFurnitureWallLogic Logic { get; private set; } = default!;

    private RoomWallItemSnapshot? _snapshot;
    private bool _dirty = true;

    private Action<long>? _onSnapshotChanged;

    public void SetPosition(string wallLocation)
    {
        WallLocation = wallLocation;

        MarkDirty();
    }

    public void SetLogic(IFurnitureWallLogic logic)
    {
        PendingStuffDataRaw = string.Empty;
        Logic = logic;
    }

    public void SetAction(Action<long>? onSnapshotChanged)
    {
        _onSnapshotChanged = onSnapshotChanged;
    }

    public void MarkDirty()
    {
        _dirty = true;
        _onSnapshotChanged?.Invoke(Id);
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
        new ItemDataUpdateMessageComposer { ObjectId = Id, State = GetSnapshot().StuffData };

    public IComposer GetRemoveComposer(long pickerId) =>
        new ItemRemoveMessageComposer { ObjectId = (int)Id, PickerId = (int)pickerId };

    private RoomWallItemSnapshot BuildSnapshot() =>
        new()
        {
            Id = Id,
            OwnerId = OwnerId,
            OwnerName = OwnerName,
            SpriteId = Definition.SpriteId,
            WallLocation = WallLocation,
            StuffData = Logic.StuffData,
            UsagePolicy = Definition.UsagePolicy,
        };
}
