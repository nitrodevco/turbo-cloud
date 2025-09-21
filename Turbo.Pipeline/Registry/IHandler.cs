using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Registry;

public interface IHandler<in T, in TContext>
{
    Task HandleAsync(T env, TContext ctx, CancellationToken ct);
}
