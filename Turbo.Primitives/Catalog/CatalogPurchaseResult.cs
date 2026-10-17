using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Catalog;

/// <summary>
/// An offer bought from the catalog and the furni the purchase put in the buyer's inventory,
/// floor and wall apart, as the purchase confirmation lists them after the offer.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CatalogPurchaseResult
{
    [Id(0)]
    public required CatalogOfferSnapshot Offer { get; init; }

    [Id(1)]
    public ImmutableArray<RoomObjectId> FloorItemIds { get; init; } = [];

    [Id(2)]
    public ImmutableArray<RoomObjectId> WallItemIds { get; init; } = [];
}
