using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Admin.Links;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// Giving other staff their way in: a setup link that makes a passkey for them, or replaces the
/// ones they lost. It needs <c>admin.passkeys.reset</c> and is bound by
/// <see cref="AdminLinkPolicy"/>. The link is shown to the admin to pass on; every one made is
/// logged with who made it.
/// </summary>
internal sealed class StaffEndpoints(
    IGrainFactory grainFactory,
    AdminLinkPolicy policy,
    IOptions<AdminConfig> config,
    ILogger<StaffEndpoints> logger
)
{
    public void Map(RouteGroupBuilder secured) =>
        secured.MapPost("/staff/passkey-links", CreateLinkAsync);

    private async Task<IResult> CreateLinkAsync(
        HttpContext http,
        PasskeyLinkRequest request,
        CancellationToken ct
    )
    {
        var identity = AdminIdentity.Of(http);
        var name = request.Name?.Trim() ?? string.Empty;
        var target = await grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerIdAsync(name, ct)
            .ConfigureAwait(false);

        if (target is not { } playerId)
            return AdminResults.Error(
                StatusCodes.Status404NotFound,
                $"No player is called {name}."
            );

        var decision = await policy
            .CheckAsync(identity.PlayerId, playerId, ct)
            .ConfigureAwait(false);

        switch (decision.Refusal)
        {
            case AdminLinkRefusal.AlreadySetUp:
                return AdminResults.Error(
                    StatusCodes.Status409Conflict,
                    "You already have a passkey. Add more on your account page."
                );
            case AdminLinkRefusal.NeedsResetNode:
                return AdminResults.Error(
                    StatusCodes.Status403Forbidden,
                    "You can't set up other players' passkeys."
                );
            case AdminLinkRefusal.OutranksIssuer:
                return AdminResults.Error(
                    StatusCodes.Status403Forbidden,
                    $"{name} has permissions you don't, so you can't set up their passkey."
                );
        }

        var link = await grainFactory
            .GetAdminAuthGrain()
            .CreateSetupTokenAsync(playerId, ct)
            .ConfigureAwait(false);
        var playerName = await grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNameAsync(playerId, ct)
            .ConfigureAwait(false);

        logger.LogInformation(
            "{Issuer} made an admin panel passkey link for {Player} ({Kind})",
            identity.Name,
            playerName,
            decision.ReplacesExisting ? "reset" : "setup"
        );

        return Results.Ok(
            new PasskeyLinkResponse(
                playerName,
                $"{config.Value.PanelUrl.TrimEnd('/')}/setup#token={link.Token}",
                link.ExpiresAtUtc,
                decision.ReplacesExisting,
                await grainFactory
                    .HasPermissionAsync(playerId, PermissionNodes.Admin.PANEL, ct)
                    .ConfigureAwait(false)
            )
        );
    }
}
