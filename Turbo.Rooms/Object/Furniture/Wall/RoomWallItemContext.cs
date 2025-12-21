using System.Threading.Tasks;
using Turbo.Primitives.Players;
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
    public override Task AddItemAsync() => SendComposerToRoomAsync(Item.GetAddComposer());

    public override Task UpdateItemAsync() => SendComposerToRoomAsync(Item.GetUpdateComposer());

    public override Task RefreshStuffDataAsync() =>
        SendComposerToRoomAsync(Item.GetRefreshStuffDataComposer());

    public override Task RemoveItemAsync(
        PlayerId pickerId,
        bool isExpired = false,
        int delay = 0
    ) => SendComposerToRoomAsync(Item.GetRemoveComposer(pickerId));
}
