using Orleans;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.StuffData;
using Turbo.Primitives.Snapshots.Furniture;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Furniture;

public class RoomItemContext<TLogic>(
    RoomGrain roomGrain,
    RoomFurniModule furniModule,
    IRoomItem<TLogic> roomItem
) : IRoomItemContext
    where TLogic : IFurnitureLogic
{
    protected readonly RoomGrain _roomGrain = roomGrain;
    protected readonly RoomFurniModule _furniModule = furniModule;
    protected readonly IRoomItem<TLogic> _roomItem = roomItem;

    public long RoomId => _roomGrain.GetPrimaryKeyLong();
    public FurnitureDefinitionSnapshot Definition => _roomItem.Definition;

    public void SetStuffData(IStuffData stuffData) => _roomItem.SetStuffData(stuffData);
}
