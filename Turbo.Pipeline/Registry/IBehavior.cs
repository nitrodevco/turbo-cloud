using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Registry;

public interface IBehavior<in T, in TContext>
{
    Task InvokeAsync(T env, TContext ctx, Func<Task> next, CancellationToken ct);
}
