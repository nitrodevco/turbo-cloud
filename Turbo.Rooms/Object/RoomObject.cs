using System;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Object;

internal abstract class RoomObject : IRoomObject
{
    public required RoomObjectId ObjectId { get; init; } = RoomObjectId.Empty;

    public int X { get; protected set; }
    public int Y { get; protected set; }
    public double Z { get; protected set; }
    public Rotation Rotation { get; protected set; }

    protected Action<RoomObjectId>? _onSnapshotChanged;
    protected bool _dirty = true;

    public bool IsDirty => _dirty;

    public void SetAction(Action<RoomObjectId>? onSnapshotChanged)
    {
        _onSnapshotChanged = onSnapshotChanged;
    }

    public void MarkDirty()
    {
        _dirty = true;
        _onSnapshotChanged?.Invoke(ObjectId);
    }
}
