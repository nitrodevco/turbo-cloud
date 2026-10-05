using System;
using System.Collections.Generic;

namespace Turbo.Primitives.Rooms;

/// <summary>
/// The <see cref="IRoomEventListener"/>s that live outside the room grain, such as a plugin's.
/// Every room grain asks it for the current set each time it publishes an event, so a listener
/// registered while rooms are loaded hears the very next event, and one disposed stops at once.
/// <para>
/// A listener runs inside the room's turn, so it must be quick and must never await a call back
/// into the room grain that is publishing to it: the room would be waiting on itself. To do
/// slow work, hand the event to a channel or a task and return. A listener that throws is
/// logged and skipped; it never stops the room or the other listeners.
/// </para>
/// </summary>
public interface IRoomEventListenerRegistry
{
    /// <summary>The listeners registered now, in registration order. Never mutated.</summary>
    public IReadOnlyList<IRoomEventListener> Listeners { get; }

    /// <summary>Registers <paramref name="listeners"/>; disposing the result removes them.</summary>
    public IDisposable Register(IEnumerable<IRoomEventListener> listeners);
}
