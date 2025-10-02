using Turbo.Contracts.Abstractions;
using Turbo.Pipeline;

namespace Turbo.Events.Registry;

public sealed class EventRegistry : EnvelopeHost<IEvent, object, EventContext>
{
    public EventRegistry()
        : base(
            new EnvelopeHostOptions<IEvent, object, EventContext>
            {
                CreateContext = (env, session) => new EventContext(),
                EnableInheritanceDispatch = true,
                HandlerMode = HandlerExecutionMode.Parallel,
                MaxHandlerDegreeOfParallelism = null,
                OnHandlerActivationError = (ex, env) => { },
                OnHandlerInvokeError = (ex, env) => { },
                OnBehaviorActivationError = (ex, env) => { },
                OnBehaviorInvokeError = (ex, env) => { },
            }
        ) { }
}
