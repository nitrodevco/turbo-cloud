using System;

namespace Turbo.Pipeline;

public sealed class EnvelopeHostOptions<TEnvelope, TMeta, TContext>
{
    public Func<TEnvelope, TMeta?, TContext> CreateContext { get; init; } = default!;
    public bool EnableInheritanceDispatch { get; init; } = true;
    public HandlerExecutionMode HandlerMode { get; init; } = HandlerExecutionMode.Parallel;
    public int? MaxHandlerDegreeOfParallelism { get; init; } = null;

    public Action<Exception, object>? OnHandlerActivationError { get; init; }
    public Action<Exception, object>? OnBehaviorActivationError { get; init; }
    public Action<Exception, object>? OnHandlerInvokeError { get; init; }
    public Action<Exception, object>? OnBehaviorInvokeError { get; init; }
}
