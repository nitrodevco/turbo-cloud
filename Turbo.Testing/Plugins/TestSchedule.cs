using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins.Scheduling;
using Turbo.Primitives.Scheduling;

namespace Turbo.Testing;

/// <summary>
/// The plugins' scheduled tasks against the hotel's clock (turbo-cloud docs/plugin-scheduler.md):
/// each is due at the next time its schedule names after it last ran (after its plugin loaded,
/// before its first run). <see cref="AdvanceAsync"/> moves the clock and runs what came due on
/// the way, once each, as the hotel's scheduler does; a run missed while the hotel was down is
/// not a thing here.
/// </summary>
public sealed class TestSchedule
{
    private readonly TestHotel _hotel;
    private readonly Dictionary<string, DateTimeOffset> _since = new(StringComparer.Ordinal);
    private readonly List<(string Key, DateTimeOffset At)> _ran = [];

    internal TestSchedule(TestHotel hotel) => _hotel = hotel;

    /// <summary>The keys of the live plugins' tasks (<c>plugin.TaskName</c>).</summary>
    public IReadOnlyList<string> Tasks => [.. _hotel.Schedules.Tasks.Select(t => t.Key)];

    /// <summary>Each run made, by key and the hotel's time, in order.</summary>
    public IReadOnlyList<(string Key, DateTimeOffset At)> Ran => _ran;

    /// <summary>When <paramref name="key"/> is due next; null when it has no schedule (run by hand only).</summary>
    public DateTimeOffset? NextRunOf(string key) =>
        ScheduleOf(Task(key)) is { } schedule
            ? schedule.Next(_since.GetValueOrDefault(key, _hotel.Clock.GetUtcNow()))
            : null;

    /// <summary>
    /// Moves the clock on by <paramref name="by"/>, running each task as it comes due on the way,
    /// in time order (a task due twice runs twice).
    /// </summary>
    public async Task AdvanceAsync(TimeSpan by, CancellationToken ct = default)
    {
        var until = _hotel.Clock.GetUtcNow() + by;

        while (Due(until) is { } next)
        {
            _hotel.Clock.Set(next.At);
            await RunAsync(next.Key, ct: ct).ConfigureAwait(false);
        }

        _hotel.Clock.Set(until);
    }

    /// <summary>Runs <paramref name="key"/> now, as its schedule or the panel's Run now (<paramref name="manual"/>) does.</summary>
    public async Task RunAsync(string key, bool manual = false, CancellationToken ct = default)
    {
        var definition = Task(key);
        var now = _hotel.Clock.GetUtcNow();

        await definition
            .Task.RunAsync(
                new ScheduledTaskRun(
                    definition.Attribute?.Name ?? definition.Task.GetType().Name,
                    now,
                    manual
                ),
                ct
            )
            .ConfigureAwait(false);

        _since[key] = now;
        _ran.Add((key, now));
    }

    internal void Loaded(string plugin)
    {
        foreach (var task in _hotel.Schedules.Tasks.Where(t => t.Plugin == plugin))
            _since.TryAdd(task.Key, _hotel.Clock.GetUtcNow());
    }

    private (string Key, DateTimeOffset At)? Due(DateTimeOffset until)
    {
        (string Key, DateTimeOffset At)? first = null;

        foreach (var key in Tasks)
        {
            if (NextRunOf(key) is { } at && at <= until && (first is null || at < first.Value.At))
                first = (key, at);
        }

        return first;
    }

    private Turbo.Operations.Scheduling.ScheduledTaskDefinition Task(string key) =>
        _hotel.Schedules.Tasks.FirstOrDefault(t => t.Key == key).Definition
        ?? throw new HotelAssertionException(
            $"No live plugin has the scheduled task {key}; it has {FakeHotelText.List(Tasks)}."
        );

    private static TaskSchedule? ScheduleOf(
        Turbo.Operations.Scheduling.ScheduledTaskDefinition definition
    ) =>
        ScheduledTasks.ScheduleOf(definition.Task) is { } schedule
            ? TaskSchedule.Parse(schedule)
            : null;
}
