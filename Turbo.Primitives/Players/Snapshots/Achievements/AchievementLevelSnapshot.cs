using Orleans;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Primitives.Players.Snapshots.Achievements;

[GenerateSerializer, Immutable]
public sealed record AchievementLevelSnapshot
{
    [Id(0)]
    public required int Level { get; init; }

    [Id(1)]
    public required int GoalCount { get; init; }

    [Id(2)]
    public required int ScoreReward { get; init; }

    [Id(3)]
    public CurrencyKind? CurrencyKind { get; init; }

    [Id(4)]
    public required int CurrencyReward { get; init; }

    [Id(5)]
    public string? BadgeCode { get; init; }
}
