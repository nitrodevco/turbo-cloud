using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Abstractions.Registry;

public interface IBehavior<in T, in TContext>
{
    Task InvokeAsync(T interaction, TContext ctx, Func<Task> next, CancellationToken ct);
}
