using System.Threading.Tasks;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.Wall;

namespace Turbo.Rooms.Furniture.Wall;

public sealed class RoomWallItem : RoomItem, IRoomWallItem
{
    public string WallLocation { get; private set; } = string.Empty;
    public IFurnitureWallLogic Logic { get; private set; } = default!;

    public void SetLogic(IFurnitureWallLogic logic)
    {
        logic.Setup(PendingStuffDataRaw);

        PendingStuffDataRaw = string.Empty;
        Logic = logic;
    }

    public Task<RoomWallItemSnapshot> GetSnapshotAsync() =>
        Task.FromResult(RoomWallItemSnapshot.FromWallItem(this));

    public Task<string> GetStuffDataAsync() => Task.FromResult(Logic.StuffData.GetLegacyString());

    public IComposer GetAddComposer() =>
        new ItemAddMessageComposer { WallItem = RoomWallItemSnapshot.FromWallItem(this) };

    public IComposer GetUpdateComposer() =>
        new ItemUpdateMessageComposer { WallItem = RoomWallItemSnapshot.FromWallItem(this) };

    public IComposer GetRefreshStuffDataComposer() =>
        new ItemDataUpdateMessageComposer
        {
            ObjectId = Id,
            State = Logic.StuffData.GetLegacyString(),
        };

    public IComposer GetRemoveComposer(long pickerId) =>
        new ItemRemoveMessageComposer { ObjectId = (int)Id, PickerId = (int)pickerId };
}
