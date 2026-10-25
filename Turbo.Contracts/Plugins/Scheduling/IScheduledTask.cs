using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Plugins.Scheduling;

/// <summary>
/// A job a plugin runs on a schedule: found in its assembly as it loads (public, built once with
/// the plugin's services), run once per cluster however many servers the hotel runs, never two
/// runs of it at once, and taken away when the plugin unloads (a run in progress is cancelled
/// through <c>ct</c>). Its schedule is a <see cref="ScheduleAttribute"/> (cron) or an
/// <see cref="EveryAttribute"/> (interval) on the class, or <see cref="Schedule"/> when staff
/// should be able to move it from a setting. The admin panel lists it with its last and next run,
/// and staff with <c>admin.plugins.manage</c> can run it now. See <c>docs/plugin-scheduler.md</c>.
/// </summary>
public interface IScheduledTask
{
    /// <summary>
    /// The schedule, read each time the next run is worked out, so a setting it comes from moves
    /// the task when staff save it: a cron expression (<c>0 18 * * MON</c>), an interval
    /// (<c>00:05:00</c>) or null for the class's attribute. A task with neither runs only when
    /// staff run it now.
    /// </summary>
    string? Schedule => null;

    /// <summary>
    /// One run. <paramref name="ct"/> is cancelled when the plugin unloads or the server stops;
    /// a run that ignores it holds the plugin's unload up.
    /// </summary>
    Task RunAsync(ScheduledTaskRun run, CancellationToken ct);
}
