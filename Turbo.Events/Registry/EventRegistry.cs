using System;
using System.Threading.Tasks;
using Turbo.Contracts.Abstractions;
using Turbo.Pipeline;

namespace Turbo.Events.Registry;

public sealed class EventRegistry(IServiceProvider sp)
    : EnvelopeHost<IEvent, object, EventContext>(
        sp,
        new EnvelopeHostOptions<IEvent, object, EventContext>
        {
            CreateContextAsync = (env, session) => Task.FromResult(new EventContext()),
            EnableInheritanceDispatch = true,
            HandlerMode = HandlerExecutionMode.Parallel,
            MaxHandlerDegreeOfParallelism = null,
            OnHandlerActivationError = (ex, env) => { },
            OnHandlerInvokeError = (ex, env) => { },
            OnBehaviorActivationError = (ex, env) => { },
            OnBehaviorInvokeError = (ex, env) => { },
        }
    ) { }
