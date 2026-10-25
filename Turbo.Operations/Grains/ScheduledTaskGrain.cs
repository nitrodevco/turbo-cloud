using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Turbo.Database.Context;
using Turbo.Database.Entities.Scheduling;
using Turbo.Primitives.Scheduling;
using Turbo.Primitives.Scheduling.Grains;

namespace Turbo.Operations.Grains;

/// <summary>
/// The scheduler, one grain for the cluster (<see cref="IScheduledTaskGrain"/>). Orleans runs one
/// activation of it, so one tick decides each run and asks one server for it: two servers with
/// the same plugin never both run a tick. It ticks once a second while any server is subscribed
/// and lets itself be collected when none is; the next subscription wakes it.
/// A run due while the one before still goes is skipped (and counted); a run due while no server
/// was up is skipped too, unless the task says <c>RunMissed</c>, when it runs once as the first
/// server with it subscribes. The next run is counted from the last one that was due, kept in
/// <c>scheduled_task_runs</c>, so a restart does not reset a weekly job's week.
/// </summary>
internal sealed class ScheduledTaskGrain(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    TimeProvider time,
    ILogger<IScheduledTaskGrain> logger
) : Grain, IScheduledTaskGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly TimeProvider _time = time;
    private readonly ILogger<IScheduledTaskGrain> _logger = logger;

    private readonly Dictionary<string, TaskState> _tasks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ServerState> _servers = new(StringComparer.Ordinal);

    // The last run of every task the database knows, read once per activation.
    private Dictionary<string, ScheduledTaskRunEntity>? _stored;

    private IDisposable? _timer;

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        StopTicking();

        return Task.CompletedTask;
    }

    public async Task SubscribeAsync(
        string serverId,
        IScheduledTaskServer server,
        ImmutableArray<ScheduledTaskEntry> tasks,
        CancellationToken ct
    )
    {
        var stored = await StoredAsync(ct);
        var now = _time.GetUtcNow();

        _servers[serverId] = new ServerState(server, now);

        var listed = tasks.Select(t => t.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var state in _tasks.Values.ToList())
            if (!listed.Contains(state.Entry.Key))
                Drop(state, serverId);

        foreach (var entry in tasks)
        {
            if (!_tasks.TryGetValue(entry.Key, out var state))
            {
                state = new TaskState(entry);
                _tasks[entry.Key] = state;

                if (stored.TryGetValue(entry.Key, out var row))
                {
                    state.LastScheduledFor = new DateTimeOffset(row.ScheduledForUtc, TimeSpan.Zero);
                    state.Last = ToResult(row);
                }

                SetSchedule(state, entry, now, firstSeen: true);
            }
            else if (!string.Equals(state.Entry.Schedule, entry.Schedule, StringComparison.Ordinal))
                SetSchedule(state, entry, now, firstSeen: false);
            else
                state.Entry = entry;

            state.Servers.Add(serverId);
        }

        StartTicking();
    }

    public Task UnsubscribeAsync(string serverId, CancellationToken ct)
    {
        if (_servers.Remove(serverId))
            foreach (var state in _tasks.Values.ToList())
                Drop(state, serverId);

        return Task.CompletedTask;
    }

    public Task<ImmutableArray<ScheduledTaskSnapshot>> ListAsync(CancellationToken ct) =>
        Task.FromResult<ImmutableArray<ScheduledTaskSnapshot>>([
            .. _tasks
                .Values.OrderBy(s => s.Entry.Key, StringComparer.Ordinal)
                .Select(s => new ScheduledTaskSnapshot
                {
                    Key = s.Entry.Key,
                    Plugin = s.Entry.Plugin,
                    Schedule = s.Entry.Schedule,
                    Description = s.Schedule?.Describe(),
                    ScheduleError = s.ScheduleError,
                    NextRunAt = s.NextRunAt,
                    LastRun = s.Last,
                    RunningSince = s.Running?.StartedAt,
                    Skipped = s.Skipped,
                    Servers = s.Servers.Count,
                }),
        ]);

    public async Task<ScheduledTaskRunNow> RunNowAsync(string key, CancellationToken ct)
    {
        if (!_tasks.TryGetValue(key, out var state))
            return ScheduledTaskRunNow.Unknown;

        if (state.Running is not null)
            return ScheduledTaskRunNow.AlreadyRunning;

        return await DispatchAsync(state, _time.GetUtcNow(), manual: true, ct)
            ? ScheduledTaskRunNow.Started
            : ScheduledTaskRunNow.NoServer;
    }

    public async Task CompletedAsync(
        string key,
        Guid runId,
        ScheduledTaskResult result,
        CancellationToken ct
    )
    {
        if (!_tasks.TryGetValue(key, out var state) || state.Running?.RunId != runId)
        {
            // A run this activation did not start (one from before Orleans moved the grain) or
            // one it has given up as lost: still the task's last run if it is the latest.
            await StoreAsync(key, null, result, ct);

            return;
        }

        var run = state.Running;

        state.Running = null;
        state.Last = result;

        if (!run.Manual)
            state.LastScheduledFor = run.ScheduledFor;

        // Its plugin unloaded while it ran.
        if (state.Servers.Count == 0)
            _tasks.Remove(key);

        await StoreAsync(key, state.LastScheduledFor, result, ct);
    }

    /// <summary>One look at the clock: drops servers not heard from, then runs what is due.</summary>
    internal async Task TickAsync(CancellationToken ct)
    {
        var now = _time.GetUtcNow();

        foreach (var (id, server) in _servers.ToList())
            if (now - server.LastSeen > ScheduledTaskTimes.ServerTimeout)
            {
                _logger.LogWarning("Scheduled tasks: server {Server} stopped answering", id);
                _servers.Remove(id);

                foreach (var state in _tasks.Values.ToList())
                {
                    if (state.Running?.ServerId == id)
                        await LoseAsync(state, now, ct);

                    Drop(state, id);
                }
            }

        foreach (var state in _tasks.Values.ToList())
        {
            if (state.NextRunAt is not { } due || due > now || state.Schedule is not { } schedule)
                continue;

            state.NextRunAt = Advance(schedule, due, now);

            if (state.Running is not null)
            {
                state.Skipped++;
                _logger.LogWarning(
                    "Scheduled task {Key}: the run due {Due:u} was skipped, the one before is still going",
                    state.Entry.Key,
                    due
                );

                continue;
            }

            await DispatchAsync(state, due, manual: false, ct);
        }

        if (_servers.Count == 0)
        {
            // Nobody to run anything: let the grain go until a server subscribes again.
            StopTicking();
            DelayDeactivation(TimeSpan.Zero);

            return;
        }

        Pin();
    }

    private async Task<bool> DispatchAsync(
        TaskState state,
        DateTimeOffset scheduledFor,
        bool manual,
        CancellationToken ct
    )
    {
        foreach (var serverId in state.Servers.Order(StringComparer.Ordinal).ToList())
        {
            if (!_servers.TryGetValue(serverId, out var server))
                continue;

            var runId = Guid.NewGuid();

            // Set first: the server may report the end before its answer to the start is read.
            state.Running = new RunState(runId, serverId, _time.GetUtcNow(), scheduledFor, manual);

            try
            {
                if (
                    await server.Reference.StartAsync(
                        state.Entry.Key,
                        runId,
                        scheduledFor,
                        manual,
                        ct
                    )
                )
                    return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Scheduled task {Key}: server {Server} could not be asked to run it",
                    state.Entry.Key,
                    serverId
                );
            }

            if (state.Running?.RunId == runId)
                state.Running = null;
        }

        _logger.LogWarning("Scheduled task {Key}: no server could run it", state.Entry.Key);

        return false;
    }

    private async Task LoseAsync(TaskState state, DateTimeOffset now, CancellationToken ct)
    {
        var run = state.Running!;
        var result = new ScheduledTaskResult(
            ScheduledTaskOutcome.Lost,
            run.StartedAt,
            (long)(now - run.StartedAt).TotalMilliseconds,
            "Its server went away while it ran.",
            run.Manual
        );

        state.Running = null;
        state.Last = result;

        if (!run.Manual)
            state.LastScheduledFor = run.ScheduledFor;

        await StoreAsync(state.Entry.Key, state.LastScheduledFor, result, ct);
    }

    private void Drop(TaskState state, string serverId)
    {
        state.Servers.Remove(serverId);

        // Its plugin is gone from every server: listed again, with its last run, once it is back.
        if (state.Servers.Count == 0 && state.Running is null)
            _tasks.Remove(state.Entry.Key);
    }

    private void SetSchedule(
        TaskState state,
        ScheduledTaskEntry entry,
        DateTimeOffset now,
        bool firstSeen
    )
    {
        state.Entry = entry;
        state.Schedule = TaskSchedule.TryParse(entry.Schedule, out var error);
        state.ScheduleError = error;

        if (error is not null)
            _logger.LogWarning(
                "Scheduled task {Key} has a schedule that can't be read, so it runs only when staff run it: {Error}",
                entry.Key,
                error
            );

        if (state.Schedule is not { } schedule)
        {
            state.NextRunAt = null;

            return;
        }

        // A schedule staff changed counts from now; one seen for the first time from its last run.
        if (!firstSeen || state.LastScheduledFor is not { } last)
        {
            state.NextRunAt = schedule.Next(now);

            return;
        }

        var due = schedule.Next(last);

        state.NextRunAt =
            due is { } d && d <= now ? (entry.RunMissed ? now : schedule.Next(now)) : due;
    }

    /// <summary>The run after <paramref name="due"/>; from now when the server fell behind, never a burst of catch-up runs.</summary>
    private static DateTimeOffset? Advance(
        TaskSchedule schedule,
        DateTimeOffset due,
        DateTimeOffset now
    ) => schedule.Next(due) is { } next && next > now ? next : schedule.Next(now);

    private async Task<Dictionary<string, ScheduledTaskRunEntity>> StoredAsync(CancellationToken ct)
    {
        if (_stored is not null)
            return _stored;

        try
        {
            await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);

            _stored = await db
                .ScheduledTaskRuns.AsNoTracking()
                .ToDictionaryAsync(r => r.Key, StringComparer.Ordinal, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Without them every task counts from now, as on a new hotel.
            _logger.LogError(ex, "Scheduled tasks: the last runs could not be read");
            _stored = new(StringComparer.Ordinal);
        }

        return _stored;
    }

    private async Task StoreAsync(
        string key,
        DateTimeOffset? scheduledFor,
        ScheduledTaskResult result,
        CancellationToken ct
    )
    {
        try
        {
            await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);
            var row = await db.ScheduledTaskRuns.FindAsync([key], ct);

            if (row is null)
            {
                row = new ScheduledTaskRunEntity
                {
                    Key = key,
                    Outcome = "",
                    ScheduledForUtc = (scheduledFor ?? result.StartedAt).UtcDateTime,
                };
                db.ScheduledTaskRuns.Add(row);
            }
            else if (row.StartedAtUtc > result.StartedAt.UtcDateTime)
                return;

            if (scheduledFor is { } due)
                row.ScheduledForUtc = due.UtcDateTime;

            row.StartedAtUtc = result.StartedAt.UtcDateTime;
            row.DurationMs = result.DurationMs;
            row.Outcome = result.Outcome.ToString();
            row.Error = result.Error is { Length: > ScheduledTaskRunEntity.ERROR_MAX_LENGTH } e
                ? e[..ScheduledTaskRunEntity.ERROR_MAX_LENGTH]
                : result.Error;
            row.Manual = result.Manual;

            await db.SaveChangesAsync(ct);

            if (_stored is not null)
                _stored[key] = row;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Scheduled task {Key}: its last run could not be stored", key);
        }
    }

    private static ScheduledTaskResult ToResult(ScheduledTaskRunEntity row) =>
        new(
            Enum.TryParse<ScheduledTaskOutcome>(row.Outcome, out var outcome)
                ? outcome
                : ScheduledTaskOutcome.Failed,
            new DateTimeOffset(row.StartedAtUtc, TimeSpan.Zero),
            row.DurationMs,
            row.Error,
            row.Manual
        );

    private void StartTicking()
    {
        Pin();

        if (_timer is not null)
            return;

        _timer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((ScheduledTaskGrain)self!).TickAsync(ct),
            this,
            new GrainTimerCreationOptions
            {
                DueTime = ScheduledTaskTimes.Tick,
                Period = ScheduledTaskTimes.Tick,
            }
        );
    }

    /// <summary>Kept while servers are subscribed: past their timeout, so a heartbeat always finds it.</summary>
    private void Pin() => DelayDeactivation(2 * ScheduledTaskTimes.ServerTimeout);

    private void StopTicking()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private sealed class TaskState(ScheduledTaskEntry entry)
    {
        public ScheduledTaskEntry Entry { get; set; } = entry;

        public TaskSchedule? Schedule { get; set; }

        public string? ScheduleError { get; set; }

        public DateTimeOffset? NextRunAt { get; set; }

        public DateTimeOffset? LastScheduledFor { get; set; }

        public ScheduledTaskResult? Last { get; set; }

        public RunState? Running { get; set; }

        public int Skipped { get; set; }

        public HashSet<string> Servers { get; } = new(StringComparer.Ordinal);
    }

    private sealed record ServerState(IScheduledTaskServer Reference, DateTimeOffset LastSeen);

    private sealed record RunState(
        Guid RunId,
        string ServerId,
        DateTimeOffset StartedAt,
        DateTimeOffset ScheduledFor,
        bool Manual
    );
}
