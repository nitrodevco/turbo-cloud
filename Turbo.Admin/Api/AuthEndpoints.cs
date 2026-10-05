using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>Signing out, and who is signed in. Signing in is <see cref="SignInEndpoints"/>.</summary>
internal sealed class AuthEndpoints(IGrainFactory grainFactory)
{
    public void Map(RouteGroupBuilder secured)
    {
        secured.MapPost("/auth/logout", LogoutAsync);
        secured.MapGet("/me", MeAsync);
    }

    private async Task<IResult> LogoutAsync(HttpContext http, CancellationToken ct)
    {
        await grainFactory
            .GetAdminAuthGrain()
            .EndSessionAsync(AdminIdentity.Of(http).SessionToken, ct)
            .ConfigureAwait(false);

        return Results.NoContent();
    }

    private async Task<IResult> MeAsync(HttpContext http, CancellationToken ct)
    {
        var identity = AdminIdentity.Of(http);

        return Results.Ok(
            new MeResponse(
                identity.PlayerId.Value,
                identity.Name,
                identity.ExpiresAtUtc,
                await grainFactory
                    .HasPermissionAsync(identity.PlayerId, PermissionNodes.Permissions.MANAGE, ct)
                    .ConfigureAwait(false),
                await grainFactory
                    .HasPermissionAsync(identity.PlayerId, PermissionNodes.Admin.PASSKEYS_RESET, ct)
                    .ConfigureAwait(false),
                await grainFactory
                    .HasPermissionAsync(identity.PlayerId, PermissionNodes.Admin.ROOMS_VIEW, ct)
                    .ConfigureAwait(false),
                await grainFactory
                    .HasPermissionAsync(
                        identity.PlayerId,
                        PermissionNodes.Admin.PERMISSIONS_VIEW,
                        ct
                    )
                    .ConfigureAwait(false),
                await grainFactory
                    .HasPermissionAsync(identity.PlayerId, PermissionNodes.Admin.PLAYERS_VIEW, ct)
                    .ConfigureAwait(false),
                await grainFactory
                    .HasPermissionAsync(
                        identity.PlayerId,
                        PermissionNodes.Admin.COMMAND_LOG_VIEW,
                        ct
                    )
                    .ConfigureAwait(false),
                await grainFactory
                    .HasPermissionAsync(identity.PlayerId, PermissionNodes.Admin.CATALOG_VIEW, ct)
                    .ConfigureAwait(false)
            )
        );
    }
}
