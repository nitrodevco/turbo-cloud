using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record ProductOfferEventMessageComposer : IComposer
{
    [Id(0)]
    public required CatalogOfferSnapshot Offer { get; init; }

    [Id(1)]
    public required List<CatalogProductSnapshot> OfferProducts { get; init; }
}
