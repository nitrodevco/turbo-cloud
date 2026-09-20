using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>
/// The club gift shelf as it stands for one player: what is on it, how many they may take, and
/// how long until the next one is theirs.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ClubGiftInfoSnapshot
{
    [Id(0)]
    public required int DaysUntilNextGift { get; init; }

    [Id(1)]
    public required int GiftsAvailable { get; init; }

    /// <summary>The offers on the shelf, drawn like any other catalog offer.</summary>
    [Id(2)]
    public required ImmutableArray<CatalogOfferSnapshot> Offers { get; init; }

    /// <summary>One entry per offer above, saying what it takes to pick it.</summary>
    [Id(3)]
    public required ImmutableArray<ClubGiftSnapshot> Gifts { get; init; }

    public static ClubGiftInfoSnapshot Empty { get; } =
        new()
        {
            DaysUntilNextGift = 0,
            GiftsAvailable = 0,
            Offers = [],
            Gifts = [],
        };
}
