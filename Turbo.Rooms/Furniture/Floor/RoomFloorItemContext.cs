using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;
using Turbo.Primitives.Orleans.Snapshots.Room.StuffData;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Furniture.Floor;

public sealed class RoomFloorItemContext(
    RoomGrain roomGrain,
    RoomFurniModule furniModule,
    IRoomFloorItem roomItem
) : RoomItemContext<IFurnitureFloorLogic>(roomGrain, furniModule, roomItem), IRoomFloorItemContext
{
    public IRoomFloorItem Item => (IRoomFloorItem)_roomItem;

    public Task RefreshItemAsync(CancellationToken ct)
    {
        _ = _roomGrain.SendComposerToRoomAsync(
            new ObjectUpdateMessageComposer
            {
                FloorItem = RoomFloorItemSnapshot.FromFloorItem(Item),
            },
            ct
        );

        return Task.CompletedTask;
    }

    public Task RefreshStuffDataAsync(CancellationToken ct)
    {
        _ = _roomGrain.SendComposerToRoomAsync(
            new ObjectDataUpdateMessageComposer
            {
                ObjectId = Item.Id,
                StuffData = StuffDataSnapshot.FromStuffData(Item.StuffData),
            },
            ct
        );

        return Task.CompletedTask;
    }
}
