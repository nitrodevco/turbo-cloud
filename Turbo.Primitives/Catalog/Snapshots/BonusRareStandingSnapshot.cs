using Orleans;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>How a bonus rare campaign has gone: players on their way, and rewards given.</summary>
[GenerateSerializer, Immutable]
public sealed record BonusRareStandingSnapshot
{
    [Id(0)]
    public required string Code { get; init; }

    /// <summary>Players with credits toward their next reward.</summary>
    [Id(1)]
    public required int PlayersInProgress { get; init; }

    [Id(2)]
    public required int RewardsGiven { get; init; }
}
