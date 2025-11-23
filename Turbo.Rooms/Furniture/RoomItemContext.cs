using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Snapshots.Furniture;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Furniture;

internal class RoomItemContext<TItem>(
    RoomGrain roomGrain,
    RoomFurniModule furniModule,
    TItem roomItem
) : IRoomItemContext
    where TItem : IRoomItem
{
    protected readonly RoomGrain _roomGrain = roomGrain;
    protected readonly RoomFurniModule _furniModule = furniModule;

    public TItem Item { get; } = roomItem;
    public long RoomId => _roomGrain.GetPrimaryKeyLong();
    public FurnitureDefinitionSnapshot Definition => Item.Definition;

    public Task PublishRoomEventAsync(RoomEvent @event, CancellationToken ct) =>
        _roomGrain.PublishRoomEventAsync(@event, ct);

    public Task SendComposerToRoomAsync(IComposer composer, CancellationToken ct) =>
        _roomGrain.SendComposerToRoomAsync(@composer, ct);
}
