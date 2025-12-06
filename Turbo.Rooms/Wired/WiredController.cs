using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events;

namespace Turbo.Rooms.Wired;

internal sealed class WiredController : IRoomEventListener
{
    public Task OnRoomEventAsync(RoomEvent @event, CancellationToken ct) =>
        HandleRoomEventAsync(@event, ct);

    private Task HandleRoomEventAsync(RoomEvent @event, CancellationToken ct)
    {
        return @event switch
        {
            FloorItemMovedEvent floorItemMovedEvent => HandleFloorItemMovedEventAsync(
                floorItemMovedEvent,
                ct
            ),
            _ => Task.CompletedTask,
        };
    }

    private Task HandleFloorItemMovedEventAsync(FloorItemMovedEvent @event, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
