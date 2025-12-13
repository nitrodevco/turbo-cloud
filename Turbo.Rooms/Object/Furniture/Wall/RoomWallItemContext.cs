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
    ) => SendComposerToRoomAsync(Item.GetRemoveComposer(pickerId), ct);
}
