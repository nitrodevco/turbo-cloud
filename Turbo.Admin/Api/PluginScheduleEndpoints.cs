using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Scheduling.Grains;

namespace Turbo.Admin.Api;

/// <summary>
/// The plugins' scheduled tasks (docs/plugin-scheduler.md), for the Plugins page's Schedule tab:
/// listed for staff with <c>admin.plugins.view</c>; running one now needs
/// <c>admin.plugins.manage</c> and goes to the command log, refused or not.
/// </summary>
internal sealed class PluginScheduleEndpoints(IGrainFactory grainFactory, AdminActionLog audit)
{
    private const string NO_ACCESS = "You can't see the server's plugins.";
    private const string NO_MANAGE = "You can't change the server's plugins.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/plugins/schedule")
            .RequirePermission(PermissionNodes.Admin.PLUGINS_VIEW, NO_ACCESS)
            .ChecksInHandler(
                "Running a task now checks admin.plugins.manage in the handler, so a refusal is written to the command log too."
            );

        group.MapGet("/", ListAsync);
        group.MapPost("/{task}/run", RunNowAsync);
    }

    private async Task<IResult> ListAsync(HttpContext http, CancellationToken ct)
    {
        var tasks = await grainFactory.GetScheduledTaskGrain().ListAsync(ct).ConfigureAwait(false);

        return Results.Ok(
            new ScheduledTasksResponse(
                [.. tasks.Select(ScheduledTaskView.From)],
                await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_MANAGE, ct)
                    .ConfigureAwait(false)
            )
        );
    }

    private async Task<IResult> RunNowAsync(string task, HttpContext http, CancellationToken ct)
    {
        var player = AdminIdentity.Of(http).PlayerId;
        var arguments = $"run {task}";

        if (!await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_MANAGE, ct).ConfigureAwait(false))
        {
            await audit
                .WriteAsync(AdminActionLog.PLUGIN, player, arguments, CommandOutcome.Refused, ct)
                .ConfigureAwait(false);

            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);
        }

        var started = await grainFactory
            .GetScheduledTaskGrain()
            .RunNowAsync(task, CancellationToken.None)
            .ConfigureAwait(false);

        await audit
            .WriteAsync(
                AdminActionLog.PLUGIN,
                player,
                arguments,
                started == ScheduledTaskRunNow.Started
                    ? CommandOutcome.Completed
                    : CommandOutcome.Failed,
                CancellationToken.None
            )
            .ConfigureAwait(false);

        return started switch
        {
            ScheduledTaskRunNow.Started => Results.Ok(
                new ScheduledTaskRunResponse(true, "Started.")
            ),
            ScheduledTaskRunNow.AlreadyRunning => AdminResults.Error(
                StatusCodes.Status409Conflict,
                "It is running now; wait for that run to end."
            ),
            ScheduledTaskRunNow.Unknown => AdminResults.Error(
                StatusCodes.Status404NotFound,
                "There is no scheduled task with that key."
            ),
            _ => AdminResults.Error(
                StatusCodes.Status503ServiceUnavailable,
                "No server that has its plugin loaded could run it."
            ),
        };
    }

    private Task<bool> HoldsAsync(HttpContext http, string node, CancellationToken ct) =>
        AdminIdentity.Of(http).HoldsAsync(grainFactory, node, ct);
}

internal sealed record ScheduledTasksResponse(
    ImmutableArray<ScheduledTaskView> Tasks,
    bool CanManage
);

internal sealed record ScheduledTaskRunResponse(bool Ok, string Message);

/// <summary>One task as the Schedule tab shows it; times in UTC, durations in milliseconds.</summary>
internal sealed record ScheduledTaskView(
    string Key,
    string Plugin,
    string? Schedule,
    string? Description,
    string? ScheduleError,
    DateTimeOffset? NextRunAt,
    DateTimeOffset? LastRunAt,
    long? LastDurationMs,
    string? LastOutcome,
    string? LastError,
    bool LastManual,
    DateTimeOffset? RunningSince,
    int Skipped,
    int Servers
)
{
    public static ScheduledTaskView From(ScheduledTaskSnapshot s) =>
        new(
            s.Key,
            s.Plugin,
            s.Schedule,
            s.Description,
            s.ScheduleError,
            s.NextRunAt,
            s.LastRun?.StartedAt,
            s.LastRun?.DurationMs,
            s.LastRun?.Outcome.ToString(),
            s.LastRun?.Error,
            s.LastRun?.Manual ?? false,
            s.RunningSince,
            s.Skipped,
            s.Servers
        );
}
