using System;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Primitives.Rooms.Object;

public interface IRoomObject<TSelf, out TLogic, out TContext> : IRoomObject
    where TSelf : IRoomObject<TSelf, TLogic, TContext>
    where TContext : IRoomObjectContext<TSelf, TLogic, TContext>
    where TLogic : IRoomObjectLogic<TSelf, TLogic, TContext>
{
    new TLogic Logic { get; }
}

public interface IMutableRoomObject<TSelf, TLogic, TContext> : IRoomObject<TSelf, TLogic, TContext>
    where TSelf : IRoomObject<TSelf, TLogic, TContext>
    where TContext : IRoomObjectContext<TSelf, TLogic, TContext>
    where TLogic : IRoomObjectLogic<TSelf, TLogic, TContext>
{
    void SetLogic(TLogic logic);
}

public interface IRoomObject
{
    public RoomObjectId ObjectId { get; }
    public int X { get; }
    public int Y { get; }
    public Altitude Z { get; }
    public Rotation Rotation { get; }
    public bool IsDirty { get; }
    public IRoomObjectLogic Logic { get; }

    public void SetAction(Action<RoomObjectId>? onSnapshotChanged);
    public void MarkDirty();
    public void SetPosition(int x, int y);
    public void SetPositionZ(Altitude z);
    public void SetRotation(Rotation rot);
    public void SetLogic(IRoomObjectLogic logic);
}
