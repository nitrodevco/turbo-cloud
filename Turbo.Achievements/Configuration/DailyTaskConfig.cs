using System;

namespace Turbo.Achievements.Configuration;

/// <summary>Daily task tunables.</summary>
public sealed class DailyTaskConfig
{
    public const string SECTION_NAME = "Turbo:DailyTasks";

    /// <summary>Regular tasks a player is given each day; the official window shows three.</summary>
    public int TasksPerDay { get; set; } = 3;

    /// <summary>
    /// When a task day starts, as a UTC time of day. The day's tasks end then and a new set is
    /// given; no official capture shows Habbo's hour.
    /// </summary>
    public TimeSpan ResetTimeUtc { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// What a Habbo Club member's ducket rewards are multiplied by. The client's daily tasks
    /// window says "Get HC membership to gain double duckets!" to a player without the club and
    /// "You get double duckets as you are an HC member!" to a member
    /// (AS3 <c>DailyTasksView.setHcDoubleDuckets</c>).
    /// </summary>
    public int HcDucketMultiplier { get; set; } = 2;

    /// <summary>
    /// Days a completed task stays claimable after its own day; the client lists such tasks
    /// under "Unclaimed tasks".
    /// </summary>
    public int UnclaimedKeepDays { get; set; } = 30;
}
