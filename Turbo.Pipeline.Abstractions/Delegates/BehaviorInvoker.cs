using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Abstractions.Delegates;

public delegate Task BehaviorInvoker<TContext>(
    object instance,
    object envelope,
    TContext ctx,
    Func<Task> next,
    CancellationToken ct
);
