using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Registry;

public interface IBehavior<in TEnvelope, in TContext>
{
    Task InvokeAsync(TEnvelope env, TContext ctx, Func<Task> next, CancellationToken ct);
}
