using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Players.Snapshots.Achievements;

[GenerateSerializer, Immutable]
public sealed record AchievementDefinitionSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required string Code { get; init; }

    [Id(2)]
    public required string Name { get; init; }

    [Id(3)]
    public required string Category { get; init; }

    [Id(4)]
    public required int MaxLevel { get; init; }

    [Id(5)]
    public required List<AchievementLevelSnapshot> Levels { get; init; }
}
