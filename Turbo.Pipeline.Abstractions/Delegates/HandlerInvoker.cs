using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Abstractions.Delegates;

public delegate Task HandlerInvoker<TContext>(
    object instance,
    object envelope,
    TContext ctx,
    CancellationToken ct
);
