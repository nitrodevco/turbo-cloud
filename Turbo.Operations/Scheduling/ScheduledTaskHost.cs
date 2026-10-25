using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Contracts.Plugins.Scheduling;
using Turbo.Plugins;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Scheduling;
using Turbo.Primitives.Scheduling.Grains;

namespace Turbo.Operations.Scheduling;

/// <summary>
/// This server's side of the scheduler: the scheduled tasks of the plugins loaded here, told to
/// the cluster's <see cref="IScheduledTaskGrain"/> as they come and go (and every few seconds
/// besides), and run here when the grain picks this server for a run. A per-server task
/// (<see cref="TaskScheduleAttribute.PerServer"/>) is run here on this server's own clock, grain
/// or no grain, and only reported to it. A plugin that starts to
/// stop has its runs cancelled and waited for first (<see cref="IPluginStopping"/>), so a run
/// never outlives the services it uses.
/// </summary>
internal sealed class ScheduledTaskHost(
    IGrainFactory grains,
    TimeProvider time,
    ILogger<ScheduledTaskHost> logger
) : BackgroundService, IScheduledTaskServer, IPluginStopping
{
    private readonly string _serverId = Guid.NewGuid().ToString("N");
    private readonly ConcurrentDictionary<string, LocalTask> _tasks = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _changed = new(0);

    private IScheduledTaskServer? _reference;

    /// <summary>The keys of the tasks loaded here, for tests and the log.</summary>
    internal IReadOnlyCollection<string> Keys => [.. _tasks.Keys];

    /// <summary>Done when every run in progress here has ended and been reported, for tests.</summary>
    internal Task IdleAsync() => Task.WhenAll(_tasks.Values.Select(t => t.Running).OfType<Task>());

    /// <summary>
    /// Adds <paramref name="tasks"/> of <paramref name="plugin"/>; disposing the result takes them
    /// away (cancelling a run in progress, which <see cref="StoppingAsync"/> has usually done).
    /// </summary>
    internal IDisposable Register(string plugin, IReadOnlyList<ScheduledTaskDefinition> tasks)
    {
        var added = new List<LocalTask>();

        foreach (var definition in tasks)
        {
            var local = new LocalTask(plugin, definition);

            if (!_tasks.TryAdd(definition.Key, local))
            {
                logger.LogWarning(
                    "Scheduled task {Key} is declared twice; the second is not run",
                    definition.Key
                );

                continue;
            }

            added.Add(local);
        }

        Changed();

        return new Registration(this, added);
    }

    public Task<bool> StartAsync(
        string key,
        Guid runId,
        DateTimeOffset scheduledFor,
        bool manual,
        CancellationToken ct
    )
    {
        if (!_tasks.TryGetValue(key, out var local) || local.Stopped.IsCancellationRequested)
            return Task.FromResult(false);

        lock (local)
        {
            if (local.Running is { IsCompleted: false })
                return Task.FromResult(false);

            // Off the caller's turn: the grain only waits for the run to start.
            local.Running = Task.Run(
                () => RunAsync(local, runId, scheduledFor, manual),
                CancellationToken.None
            );
        }

        return Task.FromResult(true);
    }

    public async Task StoppingAsync(string pluginKey, CancellationToken ct)
    {
        var stopping = _tasks.Values.Where(t => t.Plugin == pluginKey).ToList();

        if (stopping.Count == 0)
            return;

        await StopAsync(stopping, ct).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ImmutableArray<ScheduledTaskEntry> sent = [];
        var sentAt = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                RunDuePerServer();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Scheduled tasks: the per-server tasks could not be run");
            }

            try
            {
                _reference ??= grains.CreateObjectReference<IScheduledTaskServer>(this);

                var entries = Entries();
                var now = time.GetUtcNow();

                // Sent when something changed (a plugin, a schedule staff saved), else as a
                // heartbeat; a server that never had a task leaves the grain asleep.
                if (entries.IsEmpty && sent.IsEmpty)
                    sentAt = now;
                else if (
                    !entries.SequenceEqual(sent)
                    || now - sentAt >= ScheduledTaskTimes.Heartbeat
                )
                {
                    await SubscribeAsync(_reference, entries, stoppingToken).ConfigureAwait(false);

                    sent = entries;
                    sentAt = now;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // The silo may not be up yet as the server starts; the next pass tries again.
                logger.LogDebug(ex, "Scheduled tasks: could not reach the scheduler");
                sentAt = DateTimeOffset.MinValue;
            }

            try
            {
                await _changed
                    .WaitAsync(ScheduledTaskTimes.Tick, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await StopAsync(_tasks.Values.ToList(), cancellationToken).ConfigureAwait(false);

        try
        {
            await grains
                .GetScheduledTaskGrain()
                .UnsubscribeAsync(_serverId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Scheduled tasks: could not unsubscribe as the server stopped");
        }

        if (_reference is not null)
            grains.DeleteObjectReference<IScheduledTaskServer>(_reference);
    }

    public override void Dispose()
    {
        _changed.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// Tells the grain the tasks loaded here now, reachable at <paramref name="reference"/> (the
    /// server's grain observer; a test without a silo hands the host itself).
    /// </summary>
    internal Task SubscribeAsync(IScheduledTaskServer reference, CancellationToken ct) =>
        SubscribeAsync(reference, Entries(), ct);

    private Task SubscribeAsync(
        IScheduledTaskServer reference,
        ImmutableArray<ScheduledTaskEntry> entries,
        CancellationToken ct
    ) => grains.GetScheduledTaskGrain().SubscribeAsync(_serverId, reference, entries, ct);

    /// <summary>
    /// Starts every per-server task that is due on this server's clock; one still running when
    /// it falls due again skips that run. The loop calls it each tick, tests with their clock.
    /// </summary>
    internal void RunDuePerServer()
    {
        var now = time.GetUtcNow();

        foreach (var local in _tasks.Values)
        {
            if (!local.Definition.PerServer || local.Stopped.IsCancellationRequested)
                continue;

            var expression = ScheduleOf(local);

            if (
                !local.ScheduleRead
                || !string.Equals(expression, local.Expression, StringComparison.Ordinal)
            )
            {
                local.ScheduleRead = true;
                local.Expression = expression;
                local.Schedule = TaskSchedule.TryParse(expression, out _);
                local.NextRunAt = local.Schedule?.Next(now);
            }

            if (local.NextRunAt is not { } due || due > now || local.Schedule is not { } schedule)
                continue;

            local.NextRunAt =
                schedule.Next(due) is { } next && next > now ? next : schedule.Next(now);

            lock (local)
            {
                if (local.Running is { IsCompleted: false })
                {
                    logger.LogWarning(
                        "Scheduled task {Key}: the run due {Due:u} was skipped, the one before is still going",
                        local.Definition.Key,
                        due
                    );

                    continue;
                }

                local.Running = Task.Run(
                    () => RunAsync(local, Guid.NewGuid(), due, manual: false),
                    CancellationToken.None
                );
            }
        }
    }

    private ImmutableArray<ScheduledTaskEntry> Entries() =>
        [
            .. _tasks
                .Values.Where(t => !t.Stopped.IsCancellationRequested)
                .OrderBy(t => t.Definition.Key, StringComparer.Ordinal)
                .Select(t => new ScheduledTaskEntry(
                    t.Definition.Key,
                    t.Plugin,
                    ScheduleOf(t),
                    t.Definition.RunMissed,
                    t.Definition.PerServer
                )),
        ];

    private string? ScheduleOf(LocalTask local)
    {
        try
        {
            return ScheduledTasks.ScheduleOf(local.Definition.Task);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Scheduled task {Key}: its schedule could not be read",
                local.Definition.Key
            );

            return local.Definition.Attribute?.Expression;
        }
    }

    private async Task RunAsync(
        LocalTask local,
        Guid runId,
        DateTimeOffset scheduledFor,
        bool manual
    )
    {
        var key = local.Definition.Key;
        var startedAt = time.GetUtcNow();
        var watch = Stopwatch.StartNew();
        ScheduledTaskOutcome outcome;
        string? error = null;

        try
        {
            await local
                .Definition.Task.RunAsync(
                    new ScheduledTaskRun(key, scheduledFor, manual),
                    local.Stopped.Token
                )
                .ConfigureAwait(false);

            outcome = ScheduledTaskOutcome.Succeeded;
        }
        catch (OperationCanceledException) when (local.Stopped.IsCancellationRequested)
        {
            outcome = ScheduledTaskOutcome.Cancelled;
            error = "Its plugin stopped while it ran.";
        }
        catch (Exception ex)
        {
            outcome = ScheduledTaskOutcome.Failed;
            error = $"{ex.GetType().Name}: {ex.Message}";
            logger.LogError(ex, "Scheduled task {Key} failed", key);
        }

        var result = new ScheduledTaskResult(
            outcome,
            startedAt,
            watch.ElapsedMilliseconds,
            error,
            manual
        );

        // A failure is logged above with its exception. A routine run is Trace, below the
        // shipped "Turbo": "Debug": a task every few seconds would otherwise fill the log (8
        // tasks, 148 lines a minute). Its first run here, a run now and a cancel say so once.
        var first = !local.Ran;
        local.Ran = true;

        logger.Log(
            outcome != ScheduledTaskOutcome.Succeeded || manual || first
                ? LogLevel.Information
                : LogLevel.Trace,
            "Scheduled task {Key} {Outcome} in {DurationMs} ms{Manual}",
            key,
            outcome,
            result.DurationMs,
            manual ? " (run now)" : ""
        );

        try
        {
            await grains
                .GetScheduledTaskGrain()
                .CompletedAsync(key, runId, result, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Scheduled task {Key}: its result could not be reported", key);
        }
    }

    private async Task StopAsync(IReadOnlyList<LocalTask> tasks, CancellationToken ct)
    {
        foreach (var local in tasks)
            await local.Stopped.CancelAsync().ConfigureAwait(false);

        Changed();

        var running = tasks
            .Select(t => t.Running)
            .OfType<Task>()
            .Where(t => !t.IsCompleted)
            .ToList();

        if (running.Count == 0)
            return;

        var all = Task.WhenAll(running);

        try
        {
            await all.WaitAsync(ScheduledTaskTimes.CancelWait, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            logger.LogWarning(
                "Scheduled tasks {Keys} did not stop within {Wait}; the unload goes on without them",
                string.Join(
                    ", ",
                    tasks
                        .Where(t => t.Running is { IsCompleted: false })
                        .Select(t => t.Definition.Key)
                ),
                ScheduledTaskTimes.CancelWait
            );
        }
    }

    private void Remove(IEnumerable<LocalTask> tasks)
    {
        foreach (var local in tasks)
        {
            local.Stopped.Cancel();
            _tasks.TryRemove(new KeyValuePair<string, LocalTask>(local.Definition.Key, local));
        }

        Changed();
    }

    private void Changed()
    {
        // One wake is enough however many changes come before the loop reads them.
        if (_changed.CurrentCount == 0)
            _changed.Release();
    }

    private sealed class LocalTask(string plugin, ScheduledTaskDefinition definition)
    {
        public string Plugin { get; } = plugin;

        public ScheduledTaskDefinition Definition { get; } = definition;

        public CancellationTokenSource Stopped { get; } = new();

        public Task? Running { get; set; }

        /// <summary>Whether it has run here since it was registered (its first run is logged).</summary>
        public bool Ran { get; set; }

        // A per-server task's own clock: the schedule last read and the next run it gives.
        public bool ScheduleRead { get; set; }

        public string? Expression { get; set; }

        public TaskSchedule? Schedule { get; set; }

        public DateTimeOffset? NextRunAt { get; set; }
    }

    private sealed class Registration(ScheduledTaskHost host, List<LocalTask> tasks) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                host.Remove(tasks);
        }
    }
}

/// <summary>A scheduled task found in an assembly: its key, its instance and its attribute, if any.</summary>
internal sealed record ScheduledTaskDefinition(
    string Key,
    IScheduledTask Task,
    TaskScheduleAttribute? Attribute
)
{
    public bool RunMissed => Attribute?.RunMissed ?? false;

    public bool PerServer => Attribute?.PerServer ?? false;
}
