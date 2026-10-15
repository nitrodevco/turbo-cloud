using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Catalog.TargetedOffers;

/// <summary>
/// One targeted offer as one player sees it, in the order <c>TargetedOfferData.parse</c> reads it
/// (WIN63-202610131318): the player's tracking state, then the offer, with the purchase limit
/// already counting what the player bought and the seconds left until it ends (0: no end).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record TargetedOfferSnapshot
{
    [Id(0)]
    public required int TrackingState { get; init; }

    [Id(1)]
    public required int Id { get; init; }

    [Id(2)]
    public required string Identifier { get; init; }

    [Id(3)]
    public required string ProductCode { get; init; }

    [Id(4)]
    public required int PriceInCredits { get; init; }

    [Id(5)]
    public required int PriceInActivityPoints { get; init; }

    [Id(6)]
    public required int ActivityPointType { get; init; }

    [Id(7)]
    public required int PurchaseLimit { get; init; }

    [Id(8)]
    public required int SecondsToExpiry { get; init; }

    [Id(9)]
    public required string Title { get; init; }

    [Id(10)]
    public required string Description { get; init; }

    [Id(11)]
    public required string ImageUrl { get; init; }

    [Id(12)]
    public required string IconImageUrl { get; init; }

    [Id(13)]
    public required int Type { get; init; }

    [Id(14)]
    public required ImmutableArray<string> SubProductCodes { get; init; }
}
