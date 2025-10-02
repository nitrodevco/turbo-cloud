using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Registry;

public interface IHandler<in TEnvelope, in TContext>
{
    ValueTask HandleAsync(TEnvelope env, TContext ctx, CancellationToken ct);
}
