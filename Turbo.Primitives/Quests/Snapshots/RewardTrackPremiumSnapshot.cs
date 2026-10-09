using Orleans;

namespace Turbo.Primitives.Quests.Snapshots;

/// <summary>What a track's premium costs and gives.</summary>
[GenerateSerializer, Immutable]
public sealed record RewardTrackPremiumSnapshot
{
    /// <summary>
    /// What task points are multiplied by for a premium owner; the client shows it as
    /// "(boost - 1) x 100 % faster progression".
    /// </summary>
    [Id(0)]
    public required double TaskPointsBoost { get; init; }

    [Id(1)]
    public required int InstantPoints { get; init; }

    [Id(2)]
    public required int CostDiamonds { get; init; }

    [Id(3)]
    public required int CostCredits { get; init; }
}
