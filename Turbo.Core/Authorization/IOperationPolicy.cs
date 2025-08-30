using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Core.Authorization;

public interface IOperationPolicy<TContext>
{
    Task<AuthorizationResult> AuthorizeAsync(TContext ctx, CancellationToken ct);
}
