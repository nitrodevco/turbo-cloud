using Turbo.Contracts.Abstractions;
using Turbo.Events.Registry;
using Turbo.Pipeline;

namespace Turbo.Events;

internal sealed class EventFeatureProcessor(
    EventRegistry registry,
    EventInvokerFactory invokerFactory
)
    : EnvelopeFeatureProcessor<IEvent, object, EventContext>(
        registry,
        invokerFactory,
        typeof(IEventHandler<>),
        typeof(IEventBehavior<>)
    ) { }
