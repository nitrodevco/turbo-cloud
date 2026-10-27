using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms;

namespace RoomWindow;

/// <summary>
/// Which windows stand in which loaded rooms, so a packet handler can find the ones of the room a
/// player is in. The logic adds a window as it takes the furni (placed, room loaded, plugin
/// reloaded) and removes it as it lets go (picked up, room unloading). In memory only: a room's
/// windows attach again whenever it loads. One for the plugin (<see cref="RoomWindowPlugin"/>).
/// </summary>
public sealed class WindowDirectory
{
    private readonly ConcurrentDictionary<RoomId, ConcurrentDictionary<int, byte>> _rooms = new();

    public void Add(RoomId room, int windowId) => _rooms.GetOrAdd(room, _ => new())[windowId] = 0;

    public void Remove(RoomId room, int windowId)
    {
        if (_rooms.TryGetValue(room, out var windows))
            windows.TryRemove(windowId, out _);
    }

    public IReadOnlyList<int> In(RoomId room) =>
        _rooms.TryGetValue(room, out var windows) ? [.. windows.Keys.Order()] : [];
}
