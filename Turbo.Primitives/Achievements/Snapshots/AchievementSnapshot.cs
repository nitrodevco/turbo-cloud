using Orleans;
using Turbo.Primitives.Achievements.Enums;

namespace Turbo.Primitives.Achievements.Snapshots;

[GenerateSerializer, Immutable]
public sealed record AchievementSnapshot
{
    [Id(0)]
    public required int AchievementId { get; init; }

    [Id(1)]
    public required int Level { get; init; }

    [Id(2)]
    public required string BadgeId { get; init; }

    [Id(3)]
    public required int ScoreAtStartOfLevel { get; init; }

    // AS3 expects the cumulative score boundary and derives the level-local limit itself.
    [Id(4)]
    public required int ScoreLimitTotal { get; init; }

    [Id(5)]
    public required int LevelRewardPoints { get; init; }

    [Id(6)]
    public required int LevelRewardPointType { get; init; }

    // AS3 subtracts ScoreAtStartOfLevel to display progress within this level.
    [Id(7)]
    public required int CurrentPointsTotal { get; init; }

    [Id(8)]
    public required bool FinalLevel { get; init; }

    [Id(9)]
    public required string Category { get; init; }

    [Id(10)]
    public required string SubCategory { get; init; }

    [Id(11)]
    public required int LevelCount { get; init; }

    [Id(12)]
    public required int DisplayMethod { get; init; }

    [Id(13)]
    public required AchievementState State { get; init; }
}
