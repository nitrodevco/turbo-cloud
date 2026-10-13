using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Habbicons.Enums;

namespace Turbo.Primitives.Habbicons.Snapshots;

/// <summary>
/// One collection of the Habbicon shop as one player sees it (AS3 <c>_-ac</c>, parser <c>_-11f</c>):
/// its Habbicons, the reward for having them all and the price of the whole set.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HabbiconCollectionSnapshot
{
    [Id(0)]
    public required int CollectionId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    /// <summary>The player has every Habbicon of the collection.</summary>
    [Id(2)]
    public required bool Completed { get; init; }

    /// <summary>The Habbicon the collection gives when completed; 0 for none.</summary>
    [Id(3)]
    public required int RewardHabbiconId { get; init; }

    [Id(4)]
    public required HabbiconState RewardState { get; init; }

    [Id(5)]
    public required int PriceCredits { get; init; }

    [Id(6)]
    public required int PriceActivityPoints { get; init; }

    [Id(7)]
    public required int ActivityPointType { get; init; }

    [Id(8)]
    public required ImmutableArray<HabbiconSnapshot> Habbicons { get; init; }
}
