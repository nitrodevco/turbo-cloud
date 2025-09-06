using System.Threading;
using System.Threading.Tasks;
using Turbo.Core.Authorization;
using Turbo.Networking.Abstractions.Session;

namespace Turbo.Authorization.Players.Requirements;

public class NotBannedRequirement<TContext> : IRequirement<TContext>
    where TContext : ISessionContext
{
    public Task<AuthorizationResult> HandleAsync(TContext ctx, CancellationToken ct) =>
        Task.FromResult(
            true
                ? AuthorizationResult.Fail(new Failure("NotBanned", "Player is banned."))
                : AuthorizationResult.Success
        );
}
