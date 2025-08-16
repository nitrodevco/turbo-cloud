namespace Turbo.Authorization.Players.Requirements;

using System;
using System.Threading;
using System.Threading.Tasks;

using Turbo.Authorization.Players.Contexts;
using Turbo.Core.Authorization;

public sealed class NotBannedHandler
    : IRequirementHandler<NotBannedRequirement, PlayerLoginContext>
{
    public Task<AuthorizationResult> HandleAsync(NotBannedRequirement requirement, PlayerLoginContext context, CancellationToken ct = default)
        => context.IsBanned
           ? Task.FromResult(new AuthorizationResult(false, [new Failure("PLAYER_BANNED", "Player is banned.")]))
           : Task.FromResult(new AuthorizationResult(true, []));
}
