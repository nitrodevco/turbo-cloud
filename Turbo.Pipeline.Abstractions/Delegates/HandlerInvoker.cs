using System.Threading;
using System.Threading.Tasks;
using Turbo.Pipeline.Abstractions.Registry;

namespace Turbo.Pipeline.Abstractions.Delegates;

public delegate Task HandlerInvoker<TContext>(
    object handler,
    object message,
    TContext context,
    CancellationToken ct
)
    where TContext : PipelineContext;
