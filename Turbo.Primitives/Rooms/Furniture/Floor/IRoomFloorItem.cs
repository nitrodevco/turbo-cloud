using System;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Furniture.Floor;

public interface IRoomFloorItem : IRoomItem
{
    public int X { get; }
    public int Y { get; }
    public double Z { get; }
    public Rotation Rotation { get; }
    public IFurnitureFloorLogic Logic { get; }

    public void SetPosition(int x, int y, double z);
    public void SetRotation(Rotation rotation);
    public void SetLogic(IFurnitureFloorLogic logic);
    public void SetAction(Action<long>? onSnapshotChanged);
    public void MarkDirty();

    public RoomFloorItemSnapshot GetSnapshot();
    public IComposer GetAddComposer();
    public IComposer GetUpdateComposer();
    public IComposer GetRefreshStuffDataComposer();
    public IComposer GetRemoveComposer(long pickerId, bool isExpired = false, int delay = 0);
}
