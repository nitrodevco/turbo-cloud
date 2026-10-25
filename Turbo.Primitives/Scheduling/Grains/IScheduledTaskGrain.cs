using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace Turbo.Primitives.Scheduling.Grains;

/// <summary>
/// The hotel's clock for plugins' scheduled tasks, one grain for the cluster: each server tells it
/// which tasks it has loaded, and when one falls due it asks one of those servers to run it, so a
/// task runs once per tick however many servers there are, and never twice at once. It keeps each
/// task's last run in the database for the panel and for runs missed while every server was down.
/// See <c>docs/plugin-scheduler.md</c>.
/// </summary>
public interface IScheduledTaskGrain : IGrainWithStringKey
{
    /// <summary>
    /// The tasks <paramref name="server"/> has loaded now, all of them: a task it no longer lists
    /// is no longer run there. Sent again every few seconds, which also keeps the server counted
    /// as up.
    /// </summary>
    public Task SubscribeAsync(
        string serverId,
        IScheduledTaskServer server,
        ImmutableArray<ScheduledTaskEntry> tasks,
        CancellationToken ct
    );

    /// <summary>The server stops: none of its tasks run there any more.</summary>
    public Task UnsubscribeAsync(string serverId, CancellationToken ct);

    /// <summary>Every task a server has loaded, with its last and next run, for the admin panel.</summary>
    public Task<ImmutableArray<ScheduledTaskSnapshot>> ListAsync(CancellationToken ct);

    /// <summary>Runs the task now, outside its schedule; its next scheduled run stays as it was.</summary>
    public Task<ScheduledTaskRunNow> RunNowAsync(string key, CancellationToken ct);

    /// <summary>A server reports how a run it was asked for ended.</summary>
    public Task CompletedAsync(
        string key,
        Guid runId,
        ScheduledTaskResult result,
        CancellationToken ct
    );
}

/// <summary>A server's side of the scheduler: runs the tasks it has loaded when the grain says so.</summary>
public interface IScheduledTaskServer : IGrainObserver
{
    /// <summary>
    /// Starts one run and returns at once (it reports the end with
    /// <see cref="IScheduledTaskGrain.CompletedAsync"/>); false when the server no longer has the task.
    /// </summary>
    public Task<bool> StartAsync(
        string key,
        Guid runId,
        DateTimeOffset scheduledFor,
        bool manual,
        CancellationToken ct
    );
}

/// <summary>One task a server has loaded, as it tells the grain.</summary>
/// <param name="Key"><c>&lt;plugin key&gt;.&lt;name&gt;</c>.</param>
/// <param name="Plugin">The plugin it belongs to.</param>
/// <param name="Schedule">Its schedule as written (<see cref="TaskSchedule"/>); null for run-now only.</param>
/// <param name="RunMissed">Whether a run missed while no server was up runs once at the next start.</param>
[GenerateSerializer, Immutable]
public sealed record ScheduledTaskEntry(
    [property: Id(0)] string Key,
    [property: Id(1)] string Plugin,
    [property: Id(2)] string? Schedule,
    [property: Id(3)] bool RunMissed
);

[GenerateSerializer]
public enum ScheduledTaskOutcome
{
    Succeeded,
    Failed,

    /// <summary>Its plugin unloaded or its server stopped while it ran.</summary>
    Cancelled,

    /// <summary>Its server went away without saying how it ended.</summary>
    Lost,
}

/// <summary>How one run ended.</summary>
[GenerateSerializer, Immutable]
public sealed record ScheduledTaskResult(
    [property: Id(0)] ScheduledTaskOutcome Outcome,
    [property: Id(1)] DateTimeOffset StartedAt,
    [property: Id(2)] long DurationMs,
    [property: Id(3)] string? Error,
    [property: Id(4)] bool Manual
);

/// <summary>One task as the admin panel lists it.</summary>
[GenerateSerializer, Immutable]
public sealed record ScheduledTaskSnapshot
{
    [Id(0)]
    public required string Key { get; init; }

    [Id(1)]
    public required string Plugin { get; init; }

    /// <summary>The schedule as written; null for a task that runs only when staff run it now.</summary>
    [Id(2)]
    public string? Schedule { get; init; }

    /// <summary>The schedule in words ("Mondays 18:00").</summary>
    [Id(3)]
    public string? Description { get; init; }

    /// <summary>Why <see cref="Schedule"/> can't be read, when it can't: the task then runs only on run now.</summary>
    [Id(4)]
    public string? ScheduleError { get; init; }

    [Id(5)]
    public DateTimeOffset? NextRunAt { get; init; }

    [Id(6)]
    public ScheduledTaskResult? LastRun { get; init; }

    /// <summary>When the run in progress started; null when none is.</summary>
    [Id(7)]
    public DateTimeOffset? RunningSince { get; init; }

    /// <summary>Runs skipped since the server started because the one before was still going.</summary>
    [Id(8)]
    public int Skipped { get; init; }

    /// <summary>How many servers have it loaded.</summary>
    [Id(9)]
    public int Servers { get; init; }
}

[GenerateSerializer]
public enum ScheduledTaskRunNow
{
    Started,
    AlreadyRunning,
    Unknown,

    /// <summary>Every server that has it refused or could not be reached.</summary>
    NoServer,
}

/// <summary>The scheduler's own pace; not settings, as nothing about a hotel's policy turns on them.</summary>
public static class ScheduledTaskTimes
{
    /// <summary>How often the grain looks for tasks that are due.</summary>
    public static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    /// <summary>How often a server tells the grain its tasks again, whether or not they changed.</summary>
    public static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(10);

    /// <summary>A server not heard from for this long is taken as gone, and its runs as lost.</summary>
    public static readonly TimeSpan ServerTimeout = TimeSpan.FromSeconds(35);

    /// <summary>How long an unload waits for a cancelled run to end before it goes on without it.</summary>
    public static readonly TimeSpan CancelWait = TimeSpan.FromSeconds(10);
}
