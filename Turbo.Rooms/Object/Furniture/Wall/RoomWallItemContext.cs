using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Object.Furniture.Wall;

internal sealed class RoomWallItemContext(
    RoomGrain roomGrain,
    RoomFurniModule furniModule,
    IRoomWallItem roomItem
) : RoomItemContext<IRoomWallItem>(roomGrain, furniModule, roomItem), IRoomWallItemContext
{
    public Task AddItemAsync(CancellationToken ct) =>
        SendComposerToRoomAsync(Item.GetAddComposer(), ct);

    public Task UpdateItemAsync(CancellationToken ct) =>
        SendComposerToRoomAsync(Item.GetUpdateComposer(), ct);

    public Task RefreshStuffDataAsync(CancellationToken ct) =>
        SendComposerToRoomAsync(Item.GetRefreshStuffDataComposer(), ct);

    public Task RemoveItemAsync(long pickerId, CancellationToken ct) =>
        SendComposerToRoomAsync(Item.GetRemoveComposer(pickerId), ct);
}
