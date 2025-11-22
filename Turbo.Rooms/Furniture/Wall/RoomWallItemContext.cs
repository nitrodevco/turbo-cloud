using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Furniture.Wall;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Furniture.Wall;

public sealed class RoomWallItemContext(
    RoomGrain roomGrain,
    RoomFurniModule furniModule,
    IRoomWallItem roomItem
) : RoomItemContext<IRoomWallItem>(roomGrain, furniModule, roomItem), IRoomWallItemContext
{
    public void MarkItemDirty() => _furniModule.MarkItemAsDirty(Item.Id);

    public Task RefreshItemAsync(CancellationToken ct)
    {
        _ = _roomGrain.SendComposerToRoomAsync(new ItemUpdateMessageComposer { }, ct);

        return Task.CompletedTask;
    }

    public async Task RefreshStuffDataAsync(CancellationToken ct)
    {
        await RefreshItemAsync(ct);
    }
}
