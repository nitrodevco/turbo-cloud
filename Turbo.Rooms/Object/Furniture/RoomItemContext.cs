using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Object.Furniture;

internal abstract class RoomItemContext<TItem>(
    RoomGrain roomGrain,
    RoomFurniModule furniModule,
    TItem roomItem
) : RoomObjectContext(roomGrain), IRoomItemContext<TItem>
    where TItem : IRoomItem
{
    protected readonly RoomFurniModule _furniModule = furniModule;

    public override RoomObjectId ObjectId => Item.ObjectId;

    public TItem Item { get; } = roomItem;
    public FurnitureDefinitionSnapshot Definition => Item.Definition;

    public Task PublishRoomEventAsync(RoomEvent @event, CancellationToken ct) =>
        _roomGrain.PublishRoomEventAsync(@event, ct);

    public Task SendComposerToRoomAsync(IComposer composer, CancellationToken ct) =>
        _roomGrain.SendComposerToRoomAsync(@composer, ct);

    public abstract Task AddItemAsync(CancellationToken ct);

    public abstract Task UpdateItemAsync(CancellationToken ct);

    public abstract Task RefreshStuffDataAsync(CancellationToken ct);

    public abstract Task RemoveItemAsync(
        long pickerId,
        CancellationToken ct,
        bool isExpired = false,
        int delay = 0
    );
}
