using System;

namespace Turbo.Primitives.Rooms.Object;

public interface IRoomObject
{
    public RoomObjectId ObjectId { get; }
    public void SetAction(Action<RoomObjectId>? onSnapshotChanged);
    public void MarkDirty();
}
