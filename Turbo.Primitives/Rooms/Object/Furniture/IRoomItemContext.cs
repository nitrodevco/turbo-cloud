using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Events;

namespace Turbo.Primitives.Rooms.Object.Furniture;

public interface IRoomItemContext<TItem> : IRoomObjectContext
    where TItem : IRoomItem
{
    public TItem Item { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
    public Task PublishRoomEventAsync(RoomEvent @event, CancellationToken ct);
    public Task SendComposerToRoomAsync(IComposer composer, CancellationToken ct);
    public Task AddItemAsync(CancellationToken ct);
    public Task UpdateItemAsync(CancellationToken ct);
    public Task RefreshStuffDataAsync(CancellationToken ct);
    public Task RemoveItemAsync(
        long pickerId,
        CancellationToken ct,
        bool isExpired = false,
        int delay = 0
    );
}
