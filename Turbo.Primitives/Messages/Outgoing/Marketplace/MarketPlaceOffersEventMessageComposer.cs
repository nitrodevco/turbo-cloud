using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Marketplace;

[GenerateSerializer, Immutable]
public sealed record MarketPlaceOffersEventMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<MarketplaceOfferSnapshot> Offers { get; init; }

    [Id(1)]
    public required int TotalItemsFound { get; init; }
}
