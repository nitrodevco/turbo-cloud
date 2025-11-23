using System.Threading.Tasks;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.StuffData;

namespace Turbo.Rooms.Furniture.Floor;

internal sealed class RoomFloorItem : RoomItem, IRoomFloorItem
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public double Z { get; private set; }
    public Rotation Rotation { get; private set; }
    public IFurnitureFloorLogic Logic { get; private set; } = default!;

    public void SetPosition(int x, int y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public void SetRotation(Rotation rotation)
    {
        Rotation = rotation;
    }

    public void SetLogic(IFurnitureFloorLogic logic)
    {
        logic.Setup(PendingStuffDataRaw);

        PendingStuffDataRaw = string.Empty;
        Logic = logic;
    }

    public Task<RoomFloorItemSnapshot> GetSnapshotAsync() =>
        Task.FromResult(RoomFloorItemSnapshot.FromFloorItem(this));

    public Task<StuffDataSnapshot> GetStuffDataSnapshotAsync() =>
        Task.FromResult(StuffDataSnapshot.FromStuffData(Logic.StuffData));

    public IComposer GetAddComposer() =>
        new ObjectAddMessageComposer { FloorItem = RoomFloorItemSnapshot.FromFloorItem(this) };

    public IComposer GetUpdateComposer() =>
        new ObjectUpdateMessageComposer { FloorItem = RoomFloorItemSnapshot.FromFloorItem(this) };

    public IComposer GetRefreshStuffDataComposer() =>
        new ObjectDataUpdateMessageComposer
        {
            ObjectId = Id,
            StuffData = StuffDataSnapshot.FromStuffData(Logic.StuffData),
        };

    public IComposer GetRemoveComposer(long pickerId, bool isExpired = false, int delay = 0) =>
        new ObjectRemoveMessageComposer
        {
            ObjectId = Id,
            IsExpired = isExpired,
            PickerId = pickerId,
            Delay = delay,
        };
}
