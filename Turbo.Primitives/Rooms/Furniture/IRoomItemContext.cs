using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Furniture;

public interface IRoomItemContext
{
    public long RoomId { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
    public Task PublishRoomEventAsync(RoomEvent @event, CancellationToken ct);
    public Task SendComposerToRoomAsync(IComposer composer, CancellationToken ct);
}
