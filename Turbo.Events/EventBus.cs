using System;
using Microsoft.Extensions.Logging;
using Turbo.Events.Abstractions;
using Turbo.Events.Abstractions.Registry;
using Turbo.Events.Configuration;
using Turbo.Pipeline.Core.Envelope;
using Turbo.Primitives;

namespace Turbo.Events;

public class EventBus(EventConfig cfg, IServiceProvider root, ILogger<EventBus> logger)
    : EnvelopeBus<EventEnvelope, IEvent, EventContext, EventConfig>(cfg, root, logger),
        IEventBus
{
    protected override string GetKeyForEnvelope(EventEnvelope envelope)
    {
        return "";
    }

    protected override EventEnvelope CreateEnvelope(IEvent interaction, object? args, string? tag)
    {
        return new EventEnvelope { Data = interaction!, Tag = tag ?? null };
    }

    protected override Type GetHandlerForType(Type interactionType)
    {
        return typeof(IEventHandler<>).MakeGenericType(interactionType);
    }

    protected override Type GetBehaviorForType(Type interactionType)
    {
        return typeof(IEventBehavior<>).MakeGenericType(interactionType);
    }

    protected override EventContext CreateContextForEnvelope(
        EventEnvelope envelope,
        IServiceProvider sp
    )
    {
        return new EventContext { Services = sp };
    }
}
