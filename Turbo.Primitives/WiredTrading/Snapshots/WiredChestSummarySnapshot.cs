using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>
/// What a chest holds, in the shape the room needs without asking again: the counts its
/// stuff data shows, the per-type counts its conditions and scanners read synchronously, and
/// the items it shows above itself when open.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredChestSummarySnapshot
{
    [Id(0)]
    public required int ItemCount { get; init; }

    [Id(1)]
    public required int Coins { get; init; }

    [Id(2)]
    public required int CapacityLevel { get; init; }

    /// <summary>The most this chest can hold now: items for a furni chest, credits for a credit chest.</summary>
    [Id(3)]
    public required int MaxCapacity { get; init; }

    [Id(4)]
    public required ImmutableDictionary<ChestItemTypeSnapshot, int> CountsByType { get; init; }

    [Id(5)]
    public required ImmutableArray<ChestItemTypeSnapshot> Preview { get; init; }

    public static readonly WiredChestSummarySnapshot Empty = new()
    {
        ItemCount = 0,
        Coins = 0,
        CapacityLevel = 0,
        MaxCapacity = 0,
        CountsByType = ImmutableDictionary<ChestItemTypeSnapshot, int>.Empty,
        Preview = [],
    };
}
