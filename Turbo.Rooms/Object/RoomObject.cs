using System;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object;

public abstract class RoomObject<TSelf, TLogic, TContext> : IRoomObject<TSelf, TLogic, TContext>
    where TSelf : IRoomObject<TSelf, TLogic, TContext>
    where TContext : IRoomObjectContext<TSelf, TLogic, TContext>
    where TLogic : IRoomObjectLogic<TSelf, TLogic, TContext>
{
    public required RoomObjectId ObjectId { get; init; } = -1;

    public int X { get; protected set; }
    public int Y { get; protected set; }
    public double Z { get; protected set; }
    public Rotation Rotation { get; protected set; }
    public bool IsDirty => _dirty;

    public TLogic Logic { get; protected set; } = default!;

    IRoomObjectLogic IRoomObject.Logic => Logic;

    protected Action<RoomObjectId>? _onSnapshotChanged;
    protected bool _dirty = true;

    public void SetAction(Action<RoomObjectId>? onSnapshotChanged)
    {
        _onSnapshotChanged = onSnapshotChanged;
    }

    public void MarkDirty()
    {
        _dirty = true;
        _onSnapshotChanged?.Invoke(ObjectId);
    }

    public virtual void SetPosition(int x, int y, double z)
    {
        z = Math.Round(z, 2);

        if (X == x && Y == y && Z == z)
            return;

        X = x;
        Y = y;
        Z = z;

        MarkDirty();
    }

    public void SetLogic(IRoomObjectLogic logic)
    {
        Logic = (TLogic)logic;
    }
}
