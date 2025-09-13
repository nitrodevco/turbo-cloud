using System;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Abstractions;
using Turbo.Events.Abstractions;
using Turbo.Events.Abstractions.Registry;
using Turbo.Events.Configuration;
using Turbo.Pipeline.Core.Envelope;

namespace Turbo.Events;

public class EventBus(EventConfig cfg, IServiceProvider root, ILogger<EventBus> logger)
    : EnvelopeBus<EventEnvelope, IEvent, EventContext, EventConfig>(cfg, root, logger),
        IEventBus
{
    protected override string GetKeyForEnvelope(EventEnvelope envelope)
    {
        return "";
    }

    protected override EventEnvelope CreateEnvelope(IEvent interaction, object? args)
    {
        return new EventEnvelope { Data = interaction! };
    }

    protected override Type GetHandlerForType()
    {
        return typeof(IEventHandler<>);
    }

    protected override Type GetBehaviorForType()
    {
        return typeof(IEventBehavior<>);
    }

    protected override EventContext CreateContextForEnvelope(
        EventEnvelope envelope,
        IServiceProvider sp
    )
    {
        return new EventContext { Services = sp };
    }
}
