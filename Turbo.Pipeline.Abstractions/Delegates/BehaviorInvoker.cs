using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Pipeline.Abstractions.Registry;

namespace Turbo.Pipeline.Abstractions.Delegates;

public delegate Task BehaviorInvoker<TContext>(
    object handler,
    object message,
    TContext context,
    Func<Task> next,
    CancellationToken ct
)
    where TContext : PipelineContext;
