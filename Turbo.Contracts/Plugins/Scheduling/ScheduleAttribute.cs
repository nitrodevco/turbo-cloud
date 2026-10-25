using System;

namespace Turbo.Contracts.Plugins.Scheduling;

/// <summary>What every schedule attribute of an <see cref="IScheduledTask"/> says besides the times.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public abstract class TaskScheduleAttribute : Attribute
{
    /// <summary>
    /// Whether a run that fell due while no server was up runs once as one starts (default:
    /// skipped, and the next run is the next one due from then).
    /// </summary>
    public bool RunMissed { get; init; }

    /// <summary>
    /// Runs on every server that has the plugin loaded, each on its own clock, rather than once
    /// for the cluster: for work on what each server holds in its own memory (its online players,
    /// a cache it keeps). Its runs are not counted from the last one after a restart, so
    /// <see cref="RunMissed"/> does not apply. Default: once per cluster, which is what a task that
    /// changes the database or pays players wants.
    /// </summary>
    public bool PerServer { get; init; }

    /// <summary>The task's name in its key (<c>&lt;plugin key&gt;.&lt;name&gt;</c>); the class name by default.</summary>
    public string? Name { get; init; }

    /// <summary>What the schedule is, as <see cref="IScheduledTask.Schedule"/> reads.</summary>
    public abstract string Expression { get; }
}

/// <summary>
/// Runs the task when a cron expression matches: five fields, minute hour day-of-month month
/// day-of-week (<c>0 18 * * MON</c> is Mondays 18:00), with <c>*</c>, lists, ranges, steps and
/// the names <c>JAN</c>-<c>DEC</c> and <c>SUN</c>-<c>SAT</c>, or <c>@hourly</c>, <c>@daily</c>,
/// <c>@weekly</c>, <c>@monthly</c>, <c>@yearly</c>. Times are the server's own time zone unless
/// <see cref="TimeZone"/> names another.
/// </summary>
public sealed class ScheduleAttribute(string cron) : TaskScheduleAttribute
{
    public string Cron { get; } = cron;

    /// <summary>
    /// The time zone the times are in (<c>Europe/London</c>); the server's own by default. A time
    /// skipped as the clocks go forward runs once as they do; a time that comes twice as they go
    /// back runs once, unless the hour is <c>*</c>.
    /// </summary>
    public string? TimeZone { get; init; }

    public override string Expression => TimeZone is { Length: > 0 } tz ? $"{Cron} {tz}" : Cron;
}

/// <summary>
/// Runs the task every <paramref name="interval"/> (<c>00:05:00</c>, as a <see cref="TimeSpan"/>
/// reads it; at least a second), counted from the last run that was due.
/// </summary>
public sealed class EveryAttribute(string interval) : TaskScheduleAttribute
{
    public string Interval { get; } = interval;

    public override string Expression => Interval;
}
