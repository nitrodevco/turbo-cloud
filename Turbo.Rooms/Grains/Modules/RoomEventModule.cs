using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// Delivers room events to the room's own systems, then to the listeners registered in
/// <see cref="IRoomEventListenerRegistry"/> (a plugin's). The room's systems are trusted and let
/// an exception through as they always have; a registered listener is outside code, so one that
/// throws is logged and skipped and the room and the other listeners carry on. All of them run
/// inside the room's turn.
/// </summary>
public sealed class RoomEventModule(RoomGrain roomGrain) : RoomGrainComponent(roomGrain)
{
    private readonly List<IRoomEventListener> _listeners = [];

    public void Register(IRoomEventListener listener)
    {
        if (!_listeners.Contains(listener))
            _listeners.Add(listener);
    }

    public void Unregister(IRoomEventListener listener) => _listeners.Remove(listener);

    /// <summary>Whether any registered (plugin) listener would hear a published event.</summary>
    public bool HasRegisteredListeners => _roomGrain._eventListeners.Listeners is { Count: > 0 };

    public async Task PublishAsync(RoomEvent evt, CancellationToken ct)
    {
        foreach (var listener in _listeners)
            await listener.OnRoomEventAsync(evt, ct);

        await PublishToRegisteredAsync(evt, ct);
    }

    /// <summary>
    /// Delivers an observer event to the registered listeners only. Used for events the room's
    /// own systems have no use for and that can fire on every step, so nothing is built or
    /// queued for them while no listener is registered (check <see cref="HasRegisteredListeners"/>
    /// before creating the event).
    /// </summary>
    public async Task PublishToRegisteredAsync(RoomEvent evt, CancellationToken ct)
    {
        if (_roomGrain._eventListeners.Listeners is not { Count: > 0 } registered)
            return;

        foreach (var listener in registered)
        {
            try
            {
                await listener.OnRoomEventAsync(evt, ct);
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _roomGrain._logger.LogError(
                    ex,
                    "Room event listener {Listener} failed on {Event} in room {RoomId}",
                    listener.GetType().Name,
                    evt.GetType().Name,
                    _roomGrain.RoomId
                );
            }
        }
    }
}
