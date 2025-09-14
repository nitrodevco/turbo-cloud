using Turbo.Contracts.Abstractions;
using Turbo.Pipeline.Registry;

namespace Turbo.Events.Registry;

public interface IEventBehavior<in T> : IBehavior<T, EventContext>
    where T : IEvent;
