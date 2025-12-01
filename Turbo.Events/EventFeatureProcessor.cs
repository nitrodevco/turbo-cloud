using Turbo.Events.Registry;
using Turbo.Pipeline;
using Turbo.Primitives.Events;

namespace Turbo.Events;

internal sealed class EventFeatureProcessor(
    EventRegistry registry,
    EnvelopeInvokerFactory<EventContext> invokerFactory
)
    : EnvelopeFeatureProcessor<IEvent, object, EventContext>(
        registry,
        invokerFactory,
        typeof(IEventHandler<>),
        typeof(IEventBehavior<>)
    ) { }
