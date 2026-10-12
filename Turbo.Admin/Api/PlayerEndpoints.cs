using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Players;
using Turbo.Admin.Rooms;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// Finding and looking at players, for staff with <c>admin.players.view</c>, and acting on them.
/// An action (ban, silence, warn, give, ...) is the hotel's own operator command, written by
/// <see cref="PlayerActionLine"/> and run as the staff member through <see cref="PanelCommands"/>:
/// their command node, the command's guards, its notices and the command log, as in game.
/// </summary>
internal sealed class PlayerEndpoints(
    IGrainFactory grainFactory,
    AdminPlayerQueries players,
    AdminRoomVisits visits,
    AdminPlayerAudit audit,
    ICommandRegistryProvider registryProvider,
    IOperatorCommandRunner runner
)
{
    private const string NO_ACCESS = "You can't look up players.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/players").AddEndpointFilter(RequirePlayersViewAsync);

        group.MapGet("/", SearchAsync);
        group.MapGet("/abilities", AbilitiesAsync);
        group.MapGet("/{id:int}", GetAsync);
        group.MapGet("/{id:int}/inventory", InventoryAsync);
        group.MapGet("/{id:int}/visits", VisitsAsync);
        group.MapGet("/{id:int}/timeline", TimelineAsync);
        group.MapGet("/{id:int}/logins", LoginsAsync);
        group.MapGet("/{id:int}/alts", AltsAsync);
        group.MapGet("/moderation", ModerationAsync);
        group.MapPost("/{id:int}/actions", ActAsync);
    }

    /// <summary>
    /// Moderation actions, newest first: to a player (<c>target</c>), by a staff member
    /// (<c>actor</c>), in a room, of one kind; <c>before</c> is the last id of the page before.
    /// </summary>
    private async Task<IResult> ModerationAsync(
        int? target,
        int? actor,
        int? room,
        string? action,
        long? before,
        CancellationToken ct
    ) =>
        Results.Ok(
            await audit
                .ModerationAsync(target, actor, room, action, before, ct)
                .ConfigureAwait(false)
        );

    /// <summary>
    /// The player's history across the hotel's records, newest first. The command log and the
    /// chat log are in it only for staff who may read those logs.
    /// </summary>
    private async Task<IResult> TimelineAsync(
        HttpContext http,
        int id,
        DateTime? before,
        CancellationToken ct
    )
    {
        var resolved = await grainFactory
            .GetPlayerPermissionGrain(AdminIdentity.Of(http).PlayerId)
            .GetResolvedAsync(ct)
            .ConfigureAwait(false);
        var kinds =
            TimelineKinds.Moderation
            | TimelineKinds.Logins
            | TimelineKinds.Wallet
            | TimelineKinds.Furni
            | TimelineKinds.Rooms;

        if (resolved.Has(PermissionNodes.Admin.COMMAND_LOG_VIEW))
            kinds |= TimelineKinds.Commands;

        if (resolved.Has(PermissionNodes.Admin.CHATLOG_VIEW))
            kinds |= TimelineKinds.Chat;

        return Results.Ok(
            await audit.TimelineAsync(id, kinds, AsUtc(before), ct).ConfigureAwait(false)
        );
    }

    /// <summary>The player's logins; where from only for staff who may see it.</summary>
    private async Task<IResult> LoginsAsync(
        HttpContext http,
        int id,
        DateTime? before,
        CancellationToken ct
    )
    {
        var identity = await grainFactory
            .HasPermissionAsync(
                AdminIdentity.Of(http).PlayerId,
                PermissionNodes.Admin.PLAYERS_IDENTITY,
                ct
            )
            .ConfigureAwait(false);

        return Results.Ok(
            new
            {
                logins = await audit
                    .LoginsAsync(id, identity, AsUtc(before), ct)
                    .ConfigureAwait(false),
            }
        );
    }

    /// <summary>Other accounts sharing the player's addresses or machines.</summary>
    private async Task<IResult> AltsAsync(HttpContext http, int id, CancellationToken ct)
    {
        if (
            !await grainFactory
                .HasPermissionAsync(
                    AdminIdentity.Of(http).PlayerId,
                    PermissionNodes.Admin.PLAYERS_IDENTITY,
                    ct
                )
                .ConfigureAwait(false)
        )
            return AdminResults.Error(
                StatusCodes.Status403Forbidden,
                "You can't see where players log in from."
            );

        return Results.Ok(new { alts = await audit.AltsAsync(id, ct).ConfigureAwait(false) });
    }

    /// <summary>A time from the query string, which may come without a zone: taken as UTC.</summary>
    private static DateTime? AsUtc(DateTime? at) =>
        at is { } value
            ? value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : null;

    private async Task<IResult> AbilitiesAsync(HttpContext http, CancellationToken ct)
    {
        var resolved = await grainFactory
            .GetPlayerPermissionGrain(AdminIdentity.Of(http).PlayerId)
            .GetResolvedAsync(ct)
            .ConfigureAwait(false);

        return Results.Ok(
            new PlayerAbilities(
                resolved.Has(PermissionNodes.Command.BAN),
                resolved.Has(PermissionNodes.Command.UNBAN),
                resolved.Has(PermissionNodes.Command.SILENCE),
                resolved.Has(PermissionNodes.Command.TRADELOCK),
                resolved.Has(PermissionNodes.Command.DISCONNECT),
                resolved.Has(PermissionNodes.Command.WARN),
                resolved.Has(PermissionNodes.Command.ALERT),
                resolved.Has(PermissionNodes.Command.GIVE),
                resolved.Has(PermissionNodes.Command.GIVEBADGE),
                resolved.Has(PermissionNodes.Command.TAKEBADGE),
                resolved.Has(PermissionNodes.Command.GIVEITEM),
                resolved.Has(PermissionNodes.Admin.PLAYERS_CREATE),
                resolved.Has(PermissionNodes.Admin.TICKETS_ISSUE),
                resolved.Has(PermissionNodes.Admin.ACCOUNTS_MANAGE)
            )
        );
    }

    private async Task<IResult> ActAsync(
        HttpContext http,
        int id,
        PlayerActionRequest request,
        CancellationToken ct
    )
    {
        var names = await grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNamesAsync([PlayerId.Parse(id)], ct)
            .ConfigureAwait(false);

        if (!names.TryGetValue(PlayerId.Parse(id), out var name))
            return AdminResults.Error(StatusCodes.Status404NotFound, $"There is no player {id}.");

        var (line, error) = PlayerActionLine.Build(name, request);

        if (line is null)
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                error ?? "That can't be done."
            );

        return Results.Ok(
            await new PanelCommands(grainFactory, registryProvider, runner)
                .RunAsync(AdminIdentity.Of(http), line)
                .ConfigureAwait(false)
        );
    }

    private async Task<IResult> SearchAsync(
        string? q,
        string? by,
        bool? online,
        int? page,
        CancellationToken ct
    ) =>
        Results.Ok(
            await players
                .SearchAsync(q, ModeOf(by), online ?? false, page ?? 1, ct)
                .ConfigureAwait(false)
        );

    private async Task<IResult> GetAsync(int id, CancellationToken ct) =>
        await players.GetAsync(id, ct).ConfigureAwait(false) is { } player
            ? Results.Ok(player)
            : AdminResults.Error(StatusCodes.Status404NotFound, $"There is no player {id}.");

    private async Task<IResult> InventoryAsync(int id, CancellationToken ct) =>
        await players.GetInventoryAsync(id, ct).ConfigureAwait(false) is { } inventory
            ? Results.Ok(inventory)
            : AdminResults.Error(StatusCodes.Status404NotFound, $"There is no player {id}.");

    private async Task<IResult> VisitsAsync(int id, CancellationToken ct) =>
        Results.Ok(await visits.ForPlayerAsync(id, ct).ConfigureAwait(false));

    private static PlayerSearchMode ModeOf(string? by) =>
        Enum.TryParse<PlayerSearchMode>(by, ignoreCase: true, out var mode)
            ? mode
            : PlayerSearchMode.Name;

    private async ValueTask<object?> RequirePlayersViewAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;

        return await grainFactory
            .HasPermissionAsync(
                AdminIdentity.Of(http).PlayerId,
                PermissionNodes.Admin.PLAYERS_VIEW,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
