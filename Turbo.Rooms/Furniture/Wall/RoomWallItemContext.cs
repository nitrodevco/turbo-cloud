using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Furniture.Wall;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Furniture.Wall;

internal sealed class RoomWallItemContext(
    RoomGrain roomGrain,
    RoomFurniModule furniModule,
    IRoomWallItem roomItem
) : RoomItemContext<IRoomWallItem>(roomGrain, furniModule, roomItem), IRoomWallItemContext
{
    public Task MarkItemDirtyAsync() => _furniModule.MarkItemAsDirtyAsync(Item.Id);

    public Task AddItemAsync(CancellationToken ct) =>
        SendComposerToRoomAsync(Item.GetAddComposer(), ct);

    public Task UpdateItemAsync(CancellationToken ct) =>
        SendComposerToRoomAsync(Item.GetUpdateComposer(), ct);

    public Task RefreshStuffDataAsync(CancellationToken ct) =>
        SendComposerToRoomAsync(Item.GetRefreshStuffDataComposer(), ct);

    public Task RemoveItemAsync(long pickerId, CancellationToken ct = default) =>
        SendComposerToRoomAsync(Item.GetRemoveComposer(pickerId), ct);
}
