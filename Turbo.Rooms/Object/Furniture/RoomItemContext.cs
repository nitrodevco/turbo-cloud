using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
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

    public Task<RoomFloorItemSnapshot?> GetFloorItemSnapshotByIdAsync(
        RoomObjectId objectId,
        CancellationToken ct
    ) => _roomGrain.GetFloorItemSnapshotByIdAsync(objectId, ct);

    public Task PublishRoomEventAsync(RoomEvent evt, CancellationToken ct) =>
        _roomGrain.PublishRoomEventAsync(evt, ct);

    public Task SendComposerToRoomAsync(IComposer composer) =>
        _roomGrain.SendComposerToRoomAsync(composer);

    public abstract Task AddItemAsync();

    public abstract Task UpdateItemAsync();

    public abstract Task RefreshStuffDataAsync();

    public abstract Task RemoveItemAsync(PlayerId pickerId, bool isExpired = false, int delay = 0);
}
