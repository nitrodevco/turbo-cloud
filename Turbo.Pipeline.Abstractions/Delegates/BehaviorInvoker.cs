using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Pipeline.Abstractions.Registry;

namespace Turbo.Pipeline.Abstractions.Delegates;

public delegate Task BehaviorInvoker(
    object handler,
    object message,
    PipelineContext context,
    Func<Task> next,
    CancellationToken ct
);
