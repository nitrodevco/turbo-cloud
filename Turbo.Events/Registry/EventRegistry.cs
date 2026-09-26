using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Pipeline;
using Turbo.Primitives.Events;

namespace Turbo.Events.Registry;

public sealed class EventRegistry(IServiceProvider sp, ILogger<EventRegistry> logger)
    : EnvelopeHost<IEvent, object, EventContext>(
        sp,
        new EnvelopeHostOptions<IEvent, object, EventContext>
        {
            CreateContextAsync = (env, session) => Task.FromResult(new EventContext()),
            EnableInheritanceDispatch = true,
            HandlerMode = HandlerExecutionMode.Parallel,
            MaxHandlerDegreeOfParallelism = null,
        },
        logger
    ) { }
