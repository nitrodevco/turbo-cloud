using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events;

namespace Turbo.Rooms.Grains.Modules;

public sealed class RoomEventModule(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;

    private readonly List<IRoomEventListener> _listeners = [];

    public void Register(IRoomEventListener listener)
    {
        if (!_listeners.Contains(listener))
            _listeners.Add(listener);
    }

    public void Unregister(IRoomEventListener listener) => _listeners.Remove(listener);

    public async Task PublishAsync(RoomEvent evt, CancellationToken ct)
    {
        foreach (var listener in _listeners)
            await listener.OnRoomEventAsync(evt, ct);
    }
}
