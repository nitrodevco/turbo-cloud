using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Registry;

public interface IHandler<in TEnvelope, in TContext>
{
    Task HandleAsync(TEnvelope env, TContext ctx, CancellationToken ct);
}
