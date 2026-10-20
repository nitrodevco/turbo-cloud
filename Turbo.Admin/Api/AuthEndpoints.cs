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
        secured
            .MapPost("/auth/logout", LogoutAsync)
            .PanelOnly("Signing out ends the caller's own session.");
        secured
            .MapGet("/me", MeAsync)
            .PanelOnly("Who the caller is and what the panel shows them.");
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
                await identity
                    .HoldsAsync(grainFactory, PermissionNodes.Permissions.MANAGE, ct)
                    .ConfigureAwait(false),
                await identity
                    .HoldsAsync(grainFactory, PermissionNodes.Admin.PASSKEYS_RESET, ct)
                    .ConfigureAwait(false),
                await identity
                    .HoldsAsync(grainFactory, PermissionNodes.Admin.ROOMS_VIEW, ct)
                    .ConfigureAwait(false),
                await identity
                    .HoldsAsync(grainFactory, PermissionNodes.Admin.PERMISSIONS_VIEW, ct)
                    .ConfigureAwait(false),
                await identity
                    .HoldsAsync(grainFactory, PermissionNodes.Admin.PLAYERS_VIEW, ct)
                    .ConfigureAwait(false),
                await identity
                    .HoldsAsync(grainFactory, PermissionNodes.Admin.COMMAND_LOG_VIEW, ct)
                    .ConfigureAwait(false),
                await identity
                    .HoldsAsync(grainFactory, PermissionNodes.Admin.CATALOG_VIEW, ct)
                    .ConfigureAwait(false),
                await identity
                    .HoldsAsync(grainFactory, PermissionNodes.Admin.CHATLOG_VIEW, ct)
                    .ConfigureAwait(false),
                await identity
                    .HoldsAsync(grainFactory, PermissionNodes.Admin.GAMEDATA_VIEW, ct)
                    .ConfigureAwait(false),
                await identity
                    .HoldsAsync(grainFactory, PermissionNodes.Admin.SETTINGS_VIEW, ct)
                    .ConfigureAwait(false),
                await identity
                    .HoldsAsync(grainFactory, PermissionNodes.Admin.CONTENT_VIEW, ct)
                    .ConfigureAwait(false),
                await identity
                    .HoldsAsync(grainFactory, PermissionNodes.Admin.PLUGINS_VIEW, ct)
                    .ConfigureAwait(false),
                await identity
                    .HoldsAsync(grainFactory, PermissionNodes.Admin.REWARD_TRACKS_MANAGE, ct)
                    .ConfigureAwait(false),
                await identity
                    .HoldsAsync(grainFactory, PermissionNodes.Moderation.TOOL, ct)
                    .ConfigureAwait(false),
                await identity
                    .HoldsAsync(grainFactory, PermissionNodes.Admin.MESSAGES_VIEW, ct)
                    .ConfigureAwait(false)
            )
        );
    }
}
