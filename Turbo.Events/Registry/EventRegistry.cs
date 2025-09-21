using Turbo.Contracts.Abstractions;
using Turbo.Pipeline;

namespace Turbo.Events.Registry;

public sealed class EventRegistry : EnvelopeHost<IEvent, object, EventContext>
{
    public EventRegistry()
        : base((env, data) => new EventContext { }) { }
}
