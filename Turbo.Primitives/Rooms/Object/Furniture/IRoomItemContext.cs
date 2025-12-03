using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Events;

namespace Turbo.Primitives.Rooms.Object.Furniture;

public interface IRoomItemContext : IRoomObjectContext
{
    public FurnitureDefinitionSnapshot Definition { get; }
    public Task PublishRoomEventAsync(RoomEvent @event, CancellationToken ct);
    public Task SendComposerToRoomAsync(IComposer composer, CancellationToken ct);
}
