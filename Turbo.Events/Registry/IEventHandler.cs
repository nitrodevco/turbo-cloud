using Turbo.Pipeline.Registry;
using Turbo.Primitives.Events;
using Turbo.Primitives.Networking;

namespace Turbo.Events.Registry;

public interface IEventHandler<in T> : IHandler<T, EventContext>
    where T : IEvent;
