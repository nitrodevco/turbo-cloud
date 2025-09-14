using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Delegates;

internal delegate Task HandlerInvoker<TContext>(
    object instance,
    object envelope,
    TContext ctx,
    CancellationToken ct
);
