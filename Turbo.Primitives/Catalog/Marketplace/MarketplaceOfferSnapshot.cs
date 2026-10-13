using Orleans;
using Turbo.Primitives.Furniture.Snapshots.StuffData;

namespace Turbo.Primitives.Catalog.Marketplace;

/// <summary>
/// One row of a marketplace list as the client reads it (Flash <c>MarketPlaceOffersParser</c> and
/// the own offers parser): a furni type, how it is drawn, its price and how long it stays. In a
/// public list a row stands for every open offer of one kind: <see cref="OfferId"/> and
/// <see cref="Price"/> are the cheapest one's and <see cref="OfferCount"/> how many there are.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record MarketplaceOfferSnapshot
{
    [Id(0)]
    public required int OfferId { get; init; }

    /// <summary>The furni type the client draws: the definition's sprite id.</summary>
    [Id(1)]
    public required int FurniTypeId { get; init; }

    [Id(2)]
    public required MarketplaceFurniType FurniType { get; init; }

    /// <summary>A wall item's data (a poster's number); empty for floor items.</summary>
    [Id(3)]
    public required string ExtraData { get; init; }

    /// <summary>A floor item's stuff data; only its serial is written for a unique one.</summary>
    [Id(4)]
    public required StuffDataSnapshot? StuffData { get; init; }

    [Id(5)]
    public required int Price { get; init; }

    [Id(6)]
    public required MarketplaceOfferStatus Status { get; init; }

    [Id(7)]
    public required int TimeLeftMinutes { get; init; }

    /// <summary>The kind's average sale price over the configured period; 0 without sales.</summary>
    [Id(8)]
    public required int AveragePrice { get; init; }

    /// <summary>Open offers of the kind (public lists only).</summary>
    [Id(9)]
    public int OfferCount { get; init; }

    /// <summary>When a sold or expired own offer changed status, in Unix milliseconds.</summary>
    [Id(10)]
    public long StatusTime { get; init; }
}

/// <summary>
/// How an offer's furni is written (Flash <c>MarketPlaceOffersParser</c>): a floor item with its
/// stuff data, a wall item with its data string, or a limited edition with only its serial.
/// </summary>
public enum MarketplaceFurniType
{
    Floor = 1,
    Wall = 2,
    Unique = 3,
}

/// <summary>An offer's status as the client reads it (Flash <c>catalog/marketplace/_-K1l</c>: OPEN, SOLD, EXPIRED).</summary>
public enum MarketplaceOfferStatus
{
    Open = 1,
    Sold = 2,
    Expired = 3,
}
