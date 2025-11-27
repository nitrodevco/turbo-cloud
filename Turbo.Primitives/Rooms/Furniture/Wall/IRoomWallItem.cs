using System;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Furniture.Wall;

public interface IRoomWallItem : IRoomItem
{
    public string WallLocation { get; }
    public IFurnitureWallLogic Logic { get; }

    public void SetPosition(string wallLocation);
    public void SetLogic(IFurnitureWallLogic logic);
    public void SetAction(Action<long>? onSnapshotChanged);
    public void MarkDirty();

    public RoomWallItemSnapshot GetSnapshot();
    public IComposer GetAddComposer();
    public IComposer GetUpdateComposer();
    public IComposer GetRefreshStuffDataComposer();
    public IComposer GetRemoveComposer(long pickerId);
}
