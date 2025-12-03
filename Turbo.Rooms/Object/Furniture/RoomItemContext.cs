using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Object.Furniture;

internal class RoomItemContext<TItem>(
    RoomGrain roomGrain,
    RoomFurniModule furniModule,
    TItem roomItem
) : RoomObjectContext(roomGrain), IRoomItemContext
    where TItem : IRoomItem
{
    protected readonly RoomFurniModule _furniModule = furniModule;

    public TItem Item { get; } = roomItem;
    public FurnitureDefinitionSnapshot Definition => Item.Definition;

    public Task PublishRoomEventAsync(RoomEvent @event, CancellationToken ct) =>
        _roomGrain.PublishRoomEventAsync(@event, ct);

    public Task SendComposerToRoomAsync(IComposer composer, CancellationToken ct) =>
        _roomGrain.SendComposerToRoomAsync(@composer, ct);
}
