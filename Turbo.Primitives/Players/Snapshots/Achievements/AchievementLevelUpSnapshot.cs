using Orleans;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Primitives.Players.Snapshots.Achievements;

[GenerateSerializer, Immutable]
public sealed record AchievementLevelUpSnapshot
{
    [Id(0)]
    public required string Code { get; init; }

    [Id(1)]
    public required int Level { get; init; }

    [Id(2)]
    public required int Progress { get; init; }

    [Id(3)]
    public required int LevelGoal { get; init; }

    [Id(4)]
    public string? BadgeCode { get; init; }

    [Id(5)]
    public CurrencyKind? CurrencyKind { get; init; }

    [Id(6)]
    public required int CurrencyReward { get; init; }

    [Id(7)]
    public required int ScoreReward { get; init; }
}
