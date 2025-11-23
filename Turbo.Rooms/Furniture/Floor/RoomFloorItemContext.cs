using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans.Snapshots.Room.Mapping;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Furniture.Floor;

public sealed class RoomFloorItemContext(
    RoomGrain roomGrain,
    RoomFurniModule furniModule,
    IRoomFloorItem roomItem
) : RoomItemContext<IRoomFloorItem>(roomGrain, furniModule, roomItem), IRoomFloorItemContext
{
    public Task MarkItemDirtyAsync() => _furniModule.MarkItemAsDirtyAsync(Item.Id);

    public Task AddItemAsync(CancellationToken ct) =>
        _roomGrain.SendComposerToRoomAsync(Item.GetAddComposer(), ct);

    public Task UpdateItemAsync(CancellationToken ct) =>
        _roomGrain.SendComposerToRoomAsync(Item.GetUpdateComposer(), ct);

    public Task RefreshStuffDataAsync(CancellationToken ct) =>
        _roomGrain.SendComposerToRoomAsync(Item.GetRefreshStuffDataComposer(), ct);

    public Task RemoveItemAsync(
        long pickerId,
        bool isExpired = false,
        int delay = 0,
        CancellationToken ct = default
    ) => _roomGrain.SendComposerToRoomAsync(Item.GetRemoveComposer(pickerId, isExpired, delay), ct);

    public Task RefreshTileAsync() => _roomGrain.ComputeTileAsync(Item.X, Item.Y);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(CancellationToken ct) =>
        _roomGrain.GetTileSnapshotAsync(Item.X, Item.Y, ct);
}
