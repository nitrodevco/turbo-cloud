using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Registry;

public interface IBehavior<in TEnvelope, in TContext>
{
    ValueTask InvokeAsync(TEnvelope env, TContext ctx, Func<ValueTask> next, CancellationToken ct);
}
