using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins.Scheduling;
using Turbo.Primitives.Scheduling;

namespace Turbo.Testing;

/// <summary>
/// A plugin's scheduled tasks against a <see cref="FakeHotel"/>'s clock: when the server would run
/// one next, and one run as the server makes it (turbo-cloud <c>docs/plugin-scheduler.md</c>).
/// </summary>
public static class FakeHotelScheduling
{
    /// <summary>
    /// When the server would run <paramref name="task"/> next after the hotel's clock reads now, by
    /// the schedule it reads (its <c>Schedule</c>, else its attribute); null when it has none.
    /// </summary>
    public static DateTimeOffset? NextRunOf(this FakeHotel hotel, IScheduledTask task) =>
        ScheduledTasks.ScheduleOf(task) is { } schedule
            ? TaskSchedule.Parse(schedule).Next(hotel.Clock.GetUtcNow())
            : null;

    /// <summary>One run of <paramref name="task"/>, due now on the hotel's clock.</summary>
    public static Task RunAsync(
        this FakeHotel hotel,
        IScheduledTask task,
        bool manual = false,
        CancellationToken ct = default
    ) =>
        task.RunAsync(
            new ScheduledTaskRun(
                ScheduledTasks.AttributeOf(task)?.Name ?? task.GetType().Name,
                hotel.Clock.GetUtcNow(),
                manual
            ),
            ct
        );
}
