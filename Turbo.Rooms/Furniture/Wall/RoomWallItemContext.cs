using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.Wall;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Furniture.Wall;

public sealed class RoomWallItemContext(
    RoomGrain roomGrain,
    RoomFurniModule furniModule,
    IRoomWallItem roomItem
) : RoomItemContext<IFurnitureWallLogic>(roomGrain, furniModule, roomItem), IRoomWallItemContext
{
    public IRoomWallItem Item => (IRoomWallItem)_roomItem;
}
