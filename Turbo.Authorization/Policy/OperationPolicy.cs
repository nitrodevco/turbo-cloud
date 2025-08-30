using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Core.Authorization;

namespace Turbo.Authorization.Policy;

public class OperationPolicy<TContext> : IOperationPolicy<TContext>
{
    private readonly List<PolicyRunner<TContext>> _policies = new();

    protected void AllOf<TRequirement>(params TRequirement[] requirements)
        where TRequirement : IRequirement<TContext>
    {
        _policies.Add(
            async (ctx, failures, ct) =>
            {
                foreach (var requirement in requirements)
                {
                    var result = await requirement.HandleAsync(ctx, ct);

                    if (!result.Succeeded)
                    {
                        failures.AddRange(result.Failures);

                        return false;
                    }
                }

                return true;
            }
        );
    }

    protected void AnyOf<TRequirement>(params TRequirement[] requirements)
        where TRequirement : IRequirement<TContext>
    {
        _policies.Add(
            async (ctx, failures, ct) =>
            {
                foreach (var requirement in requirements)
                {
                    var result = await requirement.HandleAsync(ctx, ct);

                    if (result.Succeeded)
                        return true;

                    failures.AddRange(result.Failures);
                }

                return false;
            }
        );
    }

    protected void Not<TRequirement>(TRequirement requirement)
        where TRequirement : IRequirement<TContext>
    {
        _policies.Add(
            async (ctx, failures, ct) =>
            {
                var result = await requirement.HandleAsync(ctx, ct);

                if (result.Succeeded)
                {
                    failures.AddRange(result.Failures);

                    return false;
                }

                return true;
            }
        );
    }

    public async Task<AuthorizationResult> AuthorizeAsync(TContext ctx, CancellationToken ct)
    {
        var failures = new List<Failure>();

        foreach (var p in _policies)
        {
            if (!await p(ctx, failures, ct))
                return AuthorizationResult.Fail([.. failures]);
        }

        return AuthorizationResult.Success;
    }
}
