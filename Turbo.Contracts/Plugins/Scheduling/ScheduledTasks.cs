using System.Reflection;

namespace Turbo.Contracts.Plugins.Scheduling;

/// <summary>How the server reads a task's schedule, for its own scan and for a plugin's tests.</summary>
public static class ScheduledTasks
{
    /// <summary>The task's schedule attribute, if it has one.</summary>
    public static TaskScheduleAttribute? AttributeOf(IScheduledTask task) =>
        task.GetType().GetCustomAttribute<TaskScheduleAttribute>();

    /// <summary>
    /// The schedule the server runs <paramref name="task"/> on now: its
    /// <see cref="IScheduledTask.Schedule"/> when that says one, else its attribute's; null for a
    /// task that runs only when staff run it now.
    /// </summary>
    public static string? ScheduleOf(IScheduledTask task) =>
        task.Schedule is { Length: > 0 } fromSetting ? fromSetting : AttributeOf(task)?.Expression;
}
