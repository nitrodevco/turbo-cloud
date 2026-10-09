using Orleans;

namespace Turbo.Primitives.Quests.Snapshots;

/// <summary>A level of a reward track task: reaching the count earns the points.</summary>
[GenerateSerializer, Immutable]
public sealed record RewardTrackTaskLevelSnapshot
{
    [Id(0)]
    public required int RequiredCount { get; init; }

    [Id(1)]
    public required int PointsReward { get; init; }

    /// <summary>Counts only for a player who owns the track's premium.</summary>
    [Id(2)]
    public required bool Premium { get; init; }
}
