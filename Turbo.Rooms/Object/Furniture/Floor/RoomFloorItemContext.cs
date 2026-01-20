using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Object.Furniture.Floor;

public sealed class RoomFloorItemContext(RoomGrain roomGrain, IRoomFloorItem roomObject)
    : RoomItemContext<IRoomFloorItem, IFurnitureFloorLogic, IRoomFloorItemContext>(
        roomGrain,
        roomObject
    ),
        IRoomFloorItemContext
{
    public int GetTileIdx() => _roomGrain.ToIdx(Object.X, Object.Y);

    public int GetTileIdx(int x, int y) => _roomGrain.ToIdx(x, y);

    public override Task AddItemAsync() => SendComposerToRoomAsync(Object.GetAddComposer());

    public override Task UpdateItemAsync() => SendComposerToRoomAsync(Object.GetUpdateComposer());

    public override Task RefreshStuffDataAsync() =>
        SendComposerToRoomAsync(Object.GetRefreshStuffDataComposer());

    public override Task RemoveItemAsync(
        PlayerId pickerId,
        bool isExpired = false,
        int delay = 0
    ) => SendComposerToRoomAsync(Object.GetRemoveComposer(pickerId, isExpired, delay));

    public void RefreshTile() => _roomGrain.ComputeTile(Object.X, Object.Y);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(CancellationToken ct) =>
        _roomGrain.GetTileSnapshotAsync(Object.X, Object.Y, ct);
}
