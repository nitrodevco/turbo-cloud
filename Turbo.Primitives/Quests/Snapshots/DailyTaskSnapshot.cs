using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Quests.Enums;

namespace Turbo.Primitives.Quests.Snapshots;

/// <summary>A player's daily task as the client's <c>DailyTaskInfo</c> reads it.</summary>
[GenerateSerializer, Immutable]
public sealed record DailyTaskSnapshot
{
    [Id(0)]
    public required long TaskId { get; init; }

    /// <summary>The <c>dailytask.&lt;code&gt;.*</c> texts and the image name.</summary>
    [Id(1)]
    public required string TaskCode { get; init; }

    [Id(2)]
    public required string QuestTypeCode { get; init; }

    [Id(3)]
    public required bool IsBonus { get; init; }

    [Id(4)]
    public required string ImageVersion { get; init; }

    [Id(5)]
    public required string CatalogName { get; init; }

    [Id(6)]
    public required int RequiredRepeats { get; init; }

    [Id(7)]
    public required int Repeats { get; init; }

    [Id(8)]
    public required DailyTaskStatus Status { get; init; }

    /// <summary>
    /// Seconds until the task's day ends. Negative for a task from an earlier day, which the
    /// client lists under "Unclaimed tasks" when it is completed.
    /// </summary>
    [Id(9)]
    public required int SecondsLeft { get; init; }

    [Id(10)]
    public required ImmutableArray<DailyTaskRewardSnapshot> Rewards { get; init; }
}
