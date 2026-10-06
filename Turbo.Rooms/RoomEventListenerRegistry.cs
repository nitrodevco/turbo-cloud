using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Turbo.Primitives.Rooms;

namespace Turbo.Rooms;

/// <summary>
/// Copy-on-write: a registration builds a new array, so a room mid-publish keeps iterating the
/// set it read and never sees a half-changed list.
/// </summary>
public sealed class RoomEventListenerRegistry : IRoomEventListenerRegistry
{
    private readonly Lock _gate = new();
    private volatile IRoomEventListener[] _listeners = [];

    public IReadOnlyList<IRoomEventListener> Listeners => _listeners;

    public IDisposable Register(IEnumerable<IRoomEventListener> listeners)
    {
        var batch = listeners.ToArray();

        lock (_gate)
            _listeners = [.. _listeners, .. batch];

        return new Registration(this, batch);
    }

    private void Remove(IRoomEventListener[] batch)
    {
        lock (_gate)
            _listeners = [.. _listeners.Where(x => !batch.Contains(x))];
    }

    private sealed class Registration(RoomEventListenerRegistry owner, IRoomEventListener[] batch)
        : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.Remove(batch);
        }
    }
}
