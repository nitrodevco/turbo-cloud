using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Catalog.Marketplace;

/// <summary>
/// The price history of one furni type (Flash <c>MarketplaceItemStatsParser</c>): its average
/// sale price and open offers, then per day with sales an offset from today, the day's average
/// price and how many sold.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record MarketplaceItemStatsSnapshot
{
    [Id(0)]
    public required int AveragePrice { get; init; }

    [Id(1)]
    public required int OfferCount { get; init; }

    [Id(2)]
    public required int HistoryLength { get; init; }

    [Id(3)]
    public required ImmutableArray<MarketplaceDayStats> Days { get; init; }

    /// <summary>The request's category, echoed: the client drops stats meant for another item.</summary>
    [Id(4)]
    public required int FurniCategoryId { get; init; }

    [Id(5)]
    public required int FurniTypeId { get; init; }

    [Id(6)]
    public required int LowestCurrentPrice { get; init; }

    [Id(7)]
    public required int SuggestedPrice { get; init; }
}

[GenerateSerializer, Immutable]
public sealed record MarketplaceDayStats
{
    [Id(0)]
    public required int DayOffset { get; init; }

    [Id(1)]
    public required int AveragePrice { get; init; }

    [Id(2)]
    public required int SoldAmount { get; init; }
}
