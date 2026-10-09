using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Quests.Enums;

namespace Turbo.Primitives.Quests.Grains;

/// <summary>A player's daily tasks: the day's assignment, progress and claims.</summary>
public interface IPlayerDailyTaskGrain : IGrainWithIntegerKey
{
    /// <summary>Sends the player's tasks, giving them the day's tasks first if they have none yet.</summary>
    Task SendTasksAsync(CancellationToken ct);

    /// <summary>
    /// Grants a completed task's rewards and marks it claimed. False when the task is not the
    /// player's or not completed.
    /// </summary>
    Task<bool> ClaimAsync(long taskId, CancellationToken ct);

    /// <summary>Counts something the player did towards the day's tasks it fits.</summary>
    Task RecordActivityAsync(DailyTaskActivity activity, string value, CancellationToken ct);
}
