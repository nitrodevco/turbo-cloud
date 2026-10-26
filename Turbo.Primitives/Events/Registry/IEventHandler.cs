using Turbo.Pipeline.Registry;
using Turbo.Primitives.Events;

namespace Turbo.Events.Registry;

/// <summary>
/// Hears every <typeparamref name="T"/> published on the hotel's event system; one in a plugin's
/// assembly is found as the plugin loads. A contract, so it is in <c>Turbo.Primitives</c>; its
/// namespace is the one it had in <c>Turbo.Events</c>, which forwards it to here, so a plugin's
/// code and a plugin built before the move are unchanged.
/// </summary>
public interface IEventHandler<in T> : IHandler<T, EventContext>
    where T : IEvent;
