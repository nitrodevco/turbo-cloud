using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Core.Authorization;

namespace Turbo.Authorization;

public sealed class AuthorizationManager : IAuthorizationManager
{
    private readonly IServiceProvider _sp;

    public AuthorizationManager(IServiceProvider sp) => _sp = sp;

    public async Task<AuthorizationResult> AuthorizeAsync<TContext>(
        TContext context,
        IEnumerable<IRequirement> requirements,
        bool shortCircuitOnFailure = true,
        CancellationToken ct = default
    )
    {
        var all = new List<Failure>();

        foreach (var req in requirements)
        {
            var handlerType = typeof(IRequirementHandler<,>).MakeGenericType(
                req.GetType(),
                typeof(TContext)
            );
            var handler = _sp.GetService(handlerType);
            if (handler is null)
            {
                throw new InvalidOperationException(
                    $"No handler for {req.GetType().Name} + {typeof(TContext).Name}"
                );
            }

            var method = handlerType.GetMethod("HandleAsync")!;
            var task =
                (Task<AuthorizationResult>)
                    method.Invoke(handler, new object[] { req, context!, ct })!;
            var res = await task.ConfigureAwait(false);

            if (!res.Ok)
            {
                all.AddRange(res.Fails);
                if (shortCircuitOnFailure)
                {
                    return new AuthorizationResult(false, all.ToArray());
                }
            }
        }

        return new AuthorizationResult(true, []);
    }
}
