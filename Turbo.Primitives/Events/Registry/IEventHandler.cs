using Turbo.Pipeline.Registry;
using Turbo.Primitives.Events;

namespace Turbo.Events.Registry;

/// <summary>
/// Hears every <typeparamref name="T"/> published on the hotel's event system; one in a plugin's
/// assembly is found as the plugin loads. A contract, so it is in <c>Turbo.Primitives</c>, under
/// the namespace it had in <c>Turbo.Events</c>.
/// </summary>
public interface IEventHandler<in T> : IHandler<T, EventContext>
    where T : IEvent;
