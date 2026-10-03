using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Achievements;

[GenerateSerializer, Immutable]
public sealed record AchievementLevelDefinition
{
    [Id(0)]
    public required int Requirement { get; init; }

    [Id(1)]
    public required string BadgeCode { get; init; }

    [Id(2)]
    public int Score { get; init; } = 10;

    [Id(3)]
    public ImmutableArray<AchievementReward> Rewards { get; init; } = [];
}
