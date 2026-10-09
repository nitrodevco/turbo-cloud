namespace Turbo.Primitives.Quests.Enums;

/// <summary>
/// A daily task's state, the byte <c>DailyTaskInfo</c> reads. The client draws an active task
/// with its progress bar, a completed one with Claim, and a claimed one with Claim disabled
/// (AS3 <c>DailyTaskView.updateStatusAndRepeatsUI</c>).
/// </summary>
public enum DailyTaskStatus : byte
{
    Active = 0,
    Completed = 1,
    Claimed = 2,
}
