using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Events;

namespace Turbo.Primitives.Rooms.Object.Furniture;

public interface IRoomItemContext<TItem> : IRoomObjectContext
    where TItem : IRoomItem
{
    public TItem Item { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
    public Task PublishRoomEventAsync(RoomEvent @event, CancellationToken ct);
    public Task SendComposerToRoomAsync(IComposer composer);
    public Task AddItemAsync();
    public Task UpdateItemAsync();
    public Task RefreshStuffDataAsync();
    public Task RemoveItemAsync(PlayerId pickerId, bool isExpired = false, int delay = 0);
}
