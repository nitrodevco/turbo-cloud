using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Core.Authorization;

public interface IAuthorizationManager
{
    Task<AuthorizationResult> AuthorizeAsync<TContext>(
        TContext context,
        IOperationPolicy<TContext> policy,
        CancellationToken ct = default
    );
}
