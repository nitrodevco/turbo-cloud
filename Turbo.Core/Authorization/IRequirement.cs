using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Core.Authorization;

public interface IRequirement<TContext>
{
    public Task<AuthorizationResult> HandleAsync(TContext ctx, CancellationToken ct);
}
