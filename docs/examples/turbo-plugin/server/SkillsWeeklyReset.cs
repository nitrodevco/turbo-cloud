using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins.Scheduling;

namespace Skills;

/// <summary>
/// A scheduled task: found as the plugin loads, run once a week (Mondays 00:00, server time) on
/// one server however many the hotel runs, listed on the admin panel's Plugins page (Schedule)
/// with its last and next run and a Run now button. To let staff move it, drop the attribute and
/// read <see cref="IScheduledTask.Schedule"/> from a setting instead (turbo-cloud
/// <c>docs/plugin-scheduler.md</c>).
/// </summary>
[Schedule("0 0 * * MON")]
public sealed class SkillsWeeklyReset(SkillBook book) : IScheduledTask
{
    public Task RunAsync(ScheduledTaskRun run, CancellationToken ct)
    {
        book.Reset();

        return Task.CompletedTask;
    }
}
