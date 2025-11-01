using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Catalog;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

public record CatalogPageMessageComposer : IComposer
{
    public required CatalogTypeEnum CatalogType { get; init; }
    public required CatalogPageSnapshot Page { get; init; }
    public required List<CatalogOfferSnapshot> Offers { get; init; }
    public required Dictionary<int, List<CatalogProductSnapshot>> OfferProducts { get; init; }
    public required int OfferId { get; init; }
    public required bool AcceptSeasonCurrencyAsCredits { get; init; }
    public required List<CatalogFrontPageItemSnapshot> FrontPageItems { get; init; }
}
