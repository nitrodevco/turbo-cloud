using System;
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
using Turbo.Admin.Players;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Accounts;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Admin.Api;

/// <summary>
/// Creating players (<c>admin.players.create</c>), a player's login ticket: its status, a new one,
/// and taking it away (<c>admin.tickets.issue</c>), and how they sign in to the public site:
/// unlinking their Discord and ending their sign-ins (<c>admin.accounts.manage</c>), each under
/// <see cref="AdminTicketPolicy"/>.
/// A new ticket is shown once, in the answer to issuing it; nothing reads it back.
/// </summary>
internal sealed class PlayerAccountEndpoints(
    IGrainFactory grainFactory,
    IPlayerAccountService accounts,
    ILoginTicketService tickets,
    AdminTicketPolicy ticketPolicy,
    AdminSiteAccounts siteAccounts,
    IOptions<AdminConfig> config,
    ILogger<PlayerAccountEndpoints> logger
)
{
    private const string NO_ACCESS = "You can't look up players.";
    private const string NO_CREATE = "You can't create players.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/players").AddEndpointFilter(RequirePlayersViewAsync);

        group.MapPost("/", CreateAsync);
        group.MapGet("/{id:int}/ticket", TicketAsync);
        group.MapPost("/{id:int}/ticket", IssueAsync);
        group.MapDelete("/{id:int}/ticket", RevokeAsync);
        group.MapDelete("/{id:int}/discord", UnlinkDiscordAsync);
        group.MapDelete("/{id:int}/site-sessions", EndSiteSessionsAsync);
    }

    /// <summary>
    /// Takes the player's Discord account off them, and ends their sign-ins to the public site:
    /// they can no longer sign in there with it, and the Discord account can sign up afresh.
    /// </summary>
    private async Task<IResult> UnlinkDiscordAsync(HttpContext http, int id, CancellationToken ct)
    {
        var player = PlayerId.Parse(id);

        if (await ManageRefusalAsync(http, player, ct).ConfigureAwait(false) is { } refused)
            return refused;

        if (!await siteAccounts.UnlinkDiscordAsync(player, ct).ConfigureAwait(false))
            return AdminResults.Error(
                StatusCodes.Status404NotFound,
                "They have no Discord account linked."
            );

        logger.LogInformation(
            "Player {PlayerId} unlinked player {TargetId}'s Discord account",
            AdminIdentity.Of(http).PlayerId,
            id
        );

        return Results.NoContent();
    }

    private async Task<IResult> EndSiteSessionsAsync(HttpContext http, int id, CancellationToken ct)
    {
        var player = PlayerId.Parse(id);

        if (await ManageRefusalAsync(http, player, ct).ConfigureAwait(false) is { } refused)
            return refused;

        var ended = await siteAccounts.EndSessionsAsync(player, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Player {PlayerId} ended player {TargetId}'s {Count} public site sign-ins",
            AdminIdentity.Of(http).PlayerId,
            id,
            ended
        );

        return Results.NoContent();
    }

    private async Task<IResult?> ManageRefusalAsync(
        HttpContext http,
        PlayerId player,
        CancellationToken ct
    ) =>
        await ticketPolicy
            .RefusalAsync(
                AdminIdentity.Of(http).PlayerId,
                player,
                PermissionNodes.Admin.ACCOUNTS_MANAGE,
                "You can't manage players' accounts.",
                ct
            )
            .ConfigureAwait(false)
            is { } refused
            ? AdminResults.Error(StatusCodes.Status403Forbidden, refused)
            : null;

    private async Task<IResult> CreateAsync(
        HttpContext http,
        CreatePlayerRequest request,
        CancellationToken ct
    )
    {
        var identity = AdminIdentity.Of(http);

        if (
            !await HoldsAsync(identity.PlayerId, PermissionNodes.Admin.PLAYERS_CREATE, ct)
                .ConfigureAwait(false)
        )
            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_CREATE);

        var gender = string.Equals(request.Gender, "female", StringComparison.OrdinalIgnoreCase)
            ? AvatarGenderType.Female
            : AvatarGenderType.Male;
        var result = await accounts
            .CreateAsync(
                new NewPlayer(request.Name ?? string.Empty, request.Motto, gender, request.Figure),
                ct
            )
            .ConfigureAwait(false);

        if (result.Created is not { } id)
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                result.Error ?? "Not created."
            );

        logger.LogInformation(
            "Player {PlayerId} created player {NewPlayerId} from the admin panel",
            identity.PlayerId,
            id
        );

        return Results.Ok(new CreatedPlayerResponse(id.Value, request.Name!.Trim()));
    }

    private async Task<IResult> TicketAsync(HttpContext http, int id, CancellationToken ct)
    {
        var player = PlayerId.Parse(id);

        if (await RefusalAsync(http, player, ct).ConfigureAwait(false) is { } refused)
            return refused;

        var status = await tickets.GetAsync(player, ct).ConfigureAwait(false);

        return Results.Ok(
            new TicketStatusResponse(
                status is not null,
                status?.ExpiresAtUtc,
                status?.Reusable ?? false,
                status?.Expired ?? false
            )
        );
    }

    private async Task<IResult> IssueAsync(
        HttpContext http,
        int id,
        IssueTicketRequest request,
        CancellationToken ct
    )
    {
        var player = PlayerId.Parse(id);

        if (await RefusalAsync(http, player, ct).ConfigureAwait(false) is { } refused)
            return refused;

        var maxMinutes = config.Value.TicketMaxLifetimeDays * 24 * 60;

        if (request.LifetimeMinutes is { } minutes && (minutes < 1 || minutes > maxMinutes))
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                $"A ticket works for 1 minute to {config.Value.TicketMaxLifetimeDays} days, or for good."
            );

        var issued = await tickets
            .IssueAsync(
                player,
                request.LifetimeMinutes is { } lifetime ? TimeSpan.FromMinutes(lifetime) : null,
                request.Reusable,
                ct
            )
            .ConfigureAwait(false);

        // Who, for whom and for how long; never the ticket, which is the account.
        logger.LogInformation(
            "Player {PlayerId} issued player {TargetId} a login ticket (expires {ExpiresAt}, reusable {Reusable})",
            AdminIdentity.Of(http).PlayerId,
            id,
            issued.ExpiresAtUtc?.ToString("u") ?? "never",
            issued.Reusable
        );

        var template = config.Value.ClientLoginUrl;

        return Results.Ok(
            new IssuedTicketResponse(
                issued.Ticket,
                issued.ExpiresAtUtc,
                issued.Reusable,
                template.Contains("{ticket}", StringComparison.Ordinal)
                    ? template.Replace("{ticket}", issued.Ticket, StringComparison.Ordinal)
                    : null
            )
        );
    }

    private async Task<IResult> RevokeAsync(HttpContext http, int id, CancellationToken ct)
    {
        var player = PlayerId.Parse(id);

        if (await RefusalAsync(http, player, ct).ConfigureAwait(false) is { } refused)
            return refused;

        if (!await tickets.RevokeAsync(player, ct).ConfigureAwait(false))
            return AdminResults.Error(StatusCodes.Status404NotFound, "They have no login ticket.");

        logger.LogInformation(
            "Player {PlayerId} took away player {TargetId}'s login ticket",
            AdminIdentity.Of(http).PlayerId,
            id
        );

        return Results.NoContent();
    }

    /// <summary>The answer when the staff member may not handle this player's ticket; null when they may.</summary>
    private async Task<IResult?> RefusalAsync(
        HttpContext http,
        PlayerId player,
        CancellationToken ct
    )
    {
        var names = await grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNamesAsync([player], ct)
            .ConfigureAwait(false);

        if (!names.ContainsKey(player))
            return AdminResults.Error(
                StatusCodes.Status404NotFound,
                $"There is no player {player.Value}."
            );

        return
            await ticketPolicy
                .RefusalAsync(AdminIdentity.Of(http).PlayerId, player, ct)
                .ConfigureAwait(false)
                is { } refused
            ? AdminResults.Error(StatusCodes.Status403Forbidden, refused)
            : null;
    }

    private Task<bool> HoldsAsync(PlayerId player, string node, CancellationToken ct) =>
        grainFactory.HasPermissionAsync(player, node, ct);

    private async ValueTask<object?> RequirePlayersViewAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;

        return await HoldsAsync(
                AdminIdentity.Of(http).PlayerId,
                PermissionNodes.Admin.PLAYERS_VIEW,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
