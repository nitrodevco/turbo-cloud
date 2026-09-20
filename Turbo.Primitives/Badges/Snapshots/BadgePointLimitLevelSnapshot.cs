using Orleans;

namespace Turbo.Primitives.Badges.Snapshots;

/// <summary>One step of a badge family: the level, and the points it takes to reach it.</summary>
[GenerateSerializer, Immutable]
public sealed record BadgePointLimitLevelSnapshot
{
    [Id(0)]
    public required int Level { get; init; }

    [Id(1)]
    public required int Limit { get; init; }
}
