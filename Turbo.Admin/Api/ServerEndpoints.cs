using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Commands;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// Restarting the server from the panel, for staff with <c>admin.server.restart</c>: a countdown
/// the hotel is warned of as of a shutdown, then everyone is sent home, the grains write what they
/// hold as the host stops, and the process ends with <c>Turbo:Operations:RestartExitCode</c>.
/// Starting it again is the host's (Supervisor or systemd set to restart the process); without
/// one it stays stopped, as after a shutdown. Every request, refused ones too, goes to the command
/// log (<see cref="AdminActionLog"/>).
/// </summary>
internal sealed class ServerEndpoints(
    IGrainFactory grainFactory,
    IHotelAvailability availability,
    AdminActionLog audit
)
{
    /// <summary>Why a restart route checks in its handler: each refusal goes into the command log.</summary>
    private const string LOGGED =
        "Each refusal is written to the command log, so the handler checks admin.server.restart.";

    private const string NO_RESTART = "You can't restart the server.";
    private const string CANCEL = "cancel";

    public void Map(RouteGroupBuilder secured)
    {
        var restart = secured
            .MapGroup("/server/restart")
            .PanelOnly(
                "Any staff member sees whether a restart is coming and whether they may start one."
            );

        restart.MapGet("", StatusAsync);
        restart.MapPost("", RestartAsync).ChecksInHandler(LOGGED);
        restart.MapDelete("", CancelAsync).ChecksInHandler(LOGGED);
    }

    private async Task<IResult> StatusAsync(HttpContext http, CancellationToken ct) =>
        Results.Ok(StatusOf(await CanRestartAsync(http, ct).ConfigureAwait(false)));

    private async Task<IResult> RestartAsync(
        HttpContext http,
        ServerRestartRequest request,
        CancellationToken ct
    )
    {
        var player = AdminIdentity.Of(http).PlayerId;
        var reason = CommandText.OneLine(request.Message) ?? string.Empty;
        var arguments = (
            $"{request.Minutes?.ToString(CultureInfo.InvariantCulture)} {reason}"
        ).Trim();

        if (!await CanRestartAsync(http, ct).ConfigureAwait(false))
        {
            await audit
                .WriteAsync(AdminActionLog.RESTART, player, arguments, CommandOutcome.Refused, ct)
                .ConfigureAwait(false);

            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_RESTART);
        }

        var max = (int)availability.MaxCountdown.TotalMinutes;

        if (request.Minutes is not { } minutes || minutes < 0 || minutes > max)
        {
            await audit
                .WriteAsync(
                    AdminActionLog.RESTART,
                    player,
                    arguments,
                    CommandOutcome.BindFailed,
                    ct
                )
                .ConfigureAwait(false);

            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                $"Say how many minutes, from 0 to {max.ToString(CultureInfo.InvariantCulture)}."
            );
        }

        if (!availability.ScheduleRestart(TimeSpan.FromMinutes(minutes), reason))
        {
            await audit
                .WriteAsync(AdminActionLog.RESTART, player, arguments, CommandOutcome.Failed, ct)
                .ConfigureAwait(false);

            return AdminResults.Error(
                StatusCodes.Status409Conflict,
                "A shutdown is counting down or under way. Call it off before restarting."
            );
        }

        await audit
            .WriteAsync(AdminActionLog.RESTART, player, arguments, CommandOutcome.Completed, ct)
            .ConfigureAwait(false);

        return Results.Ok(StatusOf(true));
    }

    private async Task<IResult> CancelAsync(HttpContext http, CancellationToken ct)
    {
        var player = AdminIdentity.Of(http).PlayerId;

        if (!await CanRestartAsync(http, ct).ConfigureAwait(false))
        {
            await audit
                .WriteAsync(AdminActionLog.RESTART, player, CANCEL, CommandOutcome.Refused, ct)
                .ConfigureAwait(false);

            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_RESTART);
        }

        // Only a restart is called off here: a shutdown is the hotel controls' to call off.
        var called =
            availability.Current
                is { Phase: HotelAvailabilityPhase.ShutdownScheduled, Restart: true }
            && availability.Cancel();

        await audit
            .WriteAsync(
                AdminActionLog.RESTART,
                player,
                CANCEL,
                called ? CommandOutcome.Completed : CommandOutcome.Failed,
                ct
            )
            .ConfigureAwait(false);

        return called
            ? Results.Ok(StatusOf(true))
            : AdminResults.Error(StatusCodes.Status409Conflict, "No restart is counting down.");
    }

    private Task<bool> CanRestartAsync(HttpContext http, CancellationToken ct) =>
        AdminIdentity.Of(http).HoldsAsync(grainFactory, PermissionNodes.Admin.SERVER_RESTART, ct);

    private ServerRestartStatus StatusOf(bool canRestart)
    {
        var current = availability.Current;
        var restart = current.Restart;

        return new ServerRestartStatus(
            canRestart,
            restart && current.Phase == HotelAvailabilityPhase.ShutdownScheduled,
            restart && current.Phase == HotelAvailabilityPhase.ShuttingDown,
            restart ? current.AtUtc : null,
            restart ? current.Reason : string.Empty,
            availability.RestartExitCode,
            (int)availability.MaxCountdown.TotalMinutes
        );
    }
}
