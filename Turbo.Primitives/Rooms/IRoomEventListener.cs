using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Events;

namespace Turbo.Primitives.Rooms;

public interface IRoomEventListener
{
    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct);
}
