using Turbo.Contracts.Abstractions;
using Turbo.Pipeline.Abstractions.Registry;

namespace Turbo.Events.Registry;

public interface IEventHandler<in T> : IHandler<T, EventContext>
    where T : IEvent;
