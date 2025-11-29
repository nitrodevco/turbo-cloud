using System;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Object;

internal abstract class RoomObject : IRoomObject
{
    public required RoomObjectId ObjectId { get; init; } = RoomObjectId.Empty;

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
