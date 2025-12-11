using System;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Object;

public interface IRoomObject
{
    public RoomObjectId ObjectId { get; }
    public int X { get; }
    public int Y { get; }
    public double Z { get; }
    public Rotation Rotation { get; }
    public bool IsDirty { get; }
    public void SetAction(Action<RoomObjectId>? onSnapshotChanged);
    public void MarkDirty();
}
