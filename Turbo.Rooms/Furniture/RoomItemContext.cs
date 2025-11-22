using Orleans;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.Furniture.StuffData;
using Turbo.Primitives.Snapshots.Furniture;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Furniture;

public class RoomItemContext<TItem>(
    RoomGrain roomGrain,
    RoomFurniModule furniModule,
    TItem roomItem
) : IRoomItemContext
    where TItem : class, IRoomItem
{
    protected readonly RoomGrain _roomGrain = roomGrain;
    protected readonly RoomFurniModule _furniModule = furniModule;

    public TItem Item { get; } = roomItem;
    public long RoomId => _roomGrain.GetPrimaryKeyLong();
    public FurnitureDefinitionSnapshot Definition => Item.Definition;

    public void SetStuffData(IStuffData stuffData) => Item.SetStuffData(stuffData);
}
