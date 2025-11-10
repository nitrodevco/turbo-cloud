using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

public sealed record ProductOfferEventMessageComposer : IComposer
{
    public required CatalogOfferSnapshot Offer { get; init; }
    public required List<CatalogProductSnapshot> OfferProducts { get; init; }
}
