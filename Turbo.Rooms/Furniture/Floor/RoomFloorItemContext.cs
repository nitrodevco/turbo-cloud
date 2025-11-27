using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Furniture.Floor;

internal sealed class RoomFloorItemContext(
    RoomGrain roomGrain,
    RoomFurniModule furniModule,
    IRoomFloorItem roomItem
) : RoomItemContext<IRoomFloorItem>(roomGrain, furniModule, roomItem), IRoomFloorItemContext
{
    public Task AddItemAsync(CancellationToken ct) =>
        SendComposerToRoomAsync(Item.GetAddComposer(), ct);

    public Task UpdateItemAsync(CancellationToken ct) =>
        SendComposerToRoomAsync(Item.GetUpdateComposer(), ct);

    public Task RefreshStuffDataAsync(CancellationToken ct) =>
        SendComposerToRoomAsync(Item.GetRefreshStuffDataComposer(), ct);

    public Task RemoveItemAsync(
        long pickerId,
        bool isExpired = false,
        int delay = 0,
        CancellationToken ct = default
    ) => SendComposerToRoomAsync(Item.GetRemoveComposer(pickerId, isExpired, delay), ct);

    public Task RefreshTileAsync() => _roomGrain.ComputeTileAsync(Item.X, Item.Y);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(CancellationToken ct) =>
        _roomGrain.GetTileSnapshotAsync(Item.X, Item.Y, ct);
}
