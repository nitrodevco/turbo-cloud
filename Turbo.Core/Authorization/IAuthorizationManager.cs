namespace Turbo.Core.Authorization;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IAuthorizationManager
{
    Task<AuthorizationResult> AuthorizeAsync<TContext>(
        TContext ctx, IEnumerable<IRequirement> requirements, bool shortCircuitOnFailure = true, CancellationToken ct = default);
}
