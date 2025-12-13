using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Object.Furniture.Floor;

internal sealed class RoomFloorItemContext(
    RoomGrain roomGrain,
    RoomFurniModule furniModule,
    IRoomFloorItem roomItem
) : RoomItemContext<IRoomFloorItem>(roomGrain, furniModule, roomItem), IRoomFloorItemContext
{
    public override Task AddItemAsync(CancellationToken ct) =>
        SendComposerToRoomAsync(Item.GetAddComposer(), ct);

    public override Task UpdateItemAsync(CancellationToken ct) =>
        SendComposerToRoomAsync(Item.GetUpdateComposer(), ct);

    public override Task RefreshStuffDataAsync(CancellationToken ct) =>
        SendComposerToRoomAsync(Item.GetRefreshStuffDataComposer(), ct);

    public override Task RemoveItemAsync(
        long pickerId,
        CancellationToken ct,
        bool isExpired = false,
        int delay = 0
    ) => SendComposerToRoomAsync(Item.GetRemoveComposer(pickerId, isExpired, delay), ct);

    public void RefreshTile() => _roomGrain.ComputeTile(Item.X, Item.Y);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(CancellationToken ct) =>
        _roomGrain.GetTileSnapshotAsync(Item.X, Item.Y, ct);
}
