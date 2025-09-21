using Turbo.Contracts.Abstractions;
using Turbo.Events.Registry;
using Turbo.Pipeline;

namespace Turbo.Events;

public sealed class EventRegistry : EnvelopeHost<IEvent, object, EventContext>
{
    public EventRegistry()
        : base((env, data) => new EventContext { }) { }
}
