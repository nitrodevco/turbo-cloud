using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains.Modules;

internal sealed class RoomEventModule(
    RoomGrain roomGrain,
    RoomConfig roomConfig,
    RoomLiveState roomLiveState
) : IRoomModule
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomConfig _roomConfig = roomConfig;
    private readonly RoomLiveState _state = roomLiveState;
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
