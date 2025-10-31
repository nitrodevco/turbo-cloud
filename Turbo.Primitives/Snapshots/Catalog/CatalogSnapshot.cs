using System.Collections.Immutable;
using Turbo.Contracts.Enums.Catalog;

namespace Turbo.Primitives.Snapshots.Catalog;

public sealed record CatalogSnapshot(
    CatalogTypeEnum CatalogType,
    ImmutableDictionary<int, CatalogPageSnapshot> PagesById,
    IImmutableDictionary<int, CatalogOfferSnapshot> OffersById,
    IImmutableDictionary<int, CatalogProductSnapshot> ProductsById,
    IImmutableDictionary<int, ImmutableArray<int>> PageChildren,
    IImmutableDictionary<int, ImmutableArray<int>> PageOffers,
    IImmutableDictionary<int, ImmutableArray<int>> OfferProducts
);
