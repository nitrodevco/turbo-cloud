using Turbo.Contracts.Abstractions;
using Turbo.Pipeline.Abstractions.Registry;

namespace Turbo.Events.Registry;

public interface IEventBehavior<in T> : IBehavior<T, EventContext>
    where T : IEvent;
