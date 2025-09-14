using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Abstractions.Registry;

public interface IHandler<in T, in TContext>
{
    Task HandleAsync(T interaction, TContext ctx, CancellationToken ct);
}
