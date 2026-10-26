using Turbo.Pipeline.Registry;
using Turbo.Primitives.Events;

namespace Turbo.Events.Registry;

public interface IEventBehavior<in T> : IBehavior<T, EventContext>
    where T : IEvent;
