using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Marketplace;

[GenerateSerializer, Immutable]
public sealed record MarketPlaceOwnOffersEventMessageComposer : IComposer
{
    [Id(0)]
    public required int CreditsWaiting { get; init; }

    [Id(1)]
    public required ImmutableArray<MarketplaceOfferSnapshot> Offers { get; init; }
}
