using Orleans;
using Turbo.Primitives.Players.Enums.Achievements;

namespace Turbo.Primitives.Players.Snapshots.Achievements;

[GenerateSerializer, Immutable]
public sealed record PlayerAchievementProgressSnapshot
{
    [Id(0)]
    public required int AchievementId { get; init; }

    [Id(1)]
    public required string Code { get; init; }

    [Id(2)]
    public required string Category { get; init; }

    [Id(3)]
    public required int Level { get; init; }

    [Id(4)]
    public required int MaxLevel { get; init; }

    [Id(5)]
    public required int Progress { get; init; }

    [Id(6)]
    public required int LevelGoal { get; init; }

    [Id(7)]
    public required int NextLevelGoal { get; init; }

    [Id(8)]
    public required AchievementLevelState State { get; init; }
}
