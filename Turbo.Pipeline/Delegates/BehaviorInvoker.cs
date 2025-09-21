using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Delegates;

public delegate Task BehaviorInvoker<TContext>(
    object inst,
    object env,
    TContext ctx,
    Func<Task> next,
    CancellationToken ct
);
