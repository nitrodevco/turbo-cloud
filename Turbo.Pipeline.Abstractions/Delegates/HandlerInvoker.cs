using System.Threading;
using System.Threading.Tasks;
using Turbo.Pipeline.Abstractions.Registry;

namespace Turbo.Pipeline.Abstractions.Delegates;

public delegate Task HandlerInvoker(
    object handler,
    object message,
    PipelineContext context,
    CancellationToken ct
);
