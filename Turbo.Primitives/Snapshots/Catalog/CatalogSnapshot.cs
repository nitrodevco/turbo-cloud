using System.Collections.Immutable;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Primitives.Snapshots.Catalog;

public sealed record CatalogSnapshot(
    CatalogType CatalogType,
    ImmutableDictionary<int, CatalogPageSnapshot> PagesById,
    IImmutableDictionary<int, CatalogOfferSnapshot> OffersById,
    IImmutableDictionary<int, CatalogProductSnapshot> ProductsById,
    IImmutableDictionary<int, ImmutableArray<int>> PageChildren,
    IImmutableDictionary<int, ImmutableArray<int>> PageOffers,
    IImmutableDictionary<int, ImmutableArray<int>> OfferProducts
);
