using Turbo.Contracts.Abstractions;
using Turbo.Pipeline.Registry;

namespace Turbo.Events.Registry;

public interface IEventHandler<in T> : IHandler<T, EventContext>
    where T : IEvent;
