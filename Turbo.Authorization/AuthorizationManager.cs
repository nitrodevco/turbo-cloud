using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Core.Authorization;

namespace Turbo.Authorization;

public class AuthorizationManager(IServiceProvider sp) : IAuthorizationManager
{
    private readonly IServiceProvider _sp = sp;

    public async Task<AuthorizationResult> AuthorizeAsync<TContext>(
        TContext context,
        IOperationPolicy<TContext> policy,
        CancellationToken ct = default
    )
    {
        return await policy.AuthorizeAsync(context, ct).ConfigureAwait(false);
    }
}
