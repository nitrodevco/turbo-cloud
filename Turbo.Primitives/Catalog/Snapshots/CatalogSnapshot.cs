using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Primitives.Catalog.Snapshots;

[GenerateSerializer, Immutable]
public sealed record CatalogSnapshot
{
    [Id(0)]
    public required CatalogType CatalogType { get; init; }

    [Id(1)]
    public required int RootPageId { get; init; }

    [Id(2)]
    public required ImmutableDictionary<int, CatalogPageSnapshot> PagesById { get; init; }

    [Id(3)]
    public required IImmutableDictionary<int, CatalogOfferSnapshot> OffersById { get; init; }

    [Id(4)]
    public required IImmutableDictionary<int, CatalogProductSnapshot> ProductsById { get; init; }

    [Id(5)]
    public required IImmutableDictionary<int, ImmutableArray<int>> PageChildrenIds { get; init; }

    [Id(6)]
    public required IImmutableDictionary<int, ImmutableArray<int>> PageOfferIds { get; init; }

    [Id(7)]
    public required IImmutableDictionary<int, ImmutableArray<int>> OfferProductIds { get; init; }

    public static CatalogSnapshot Empty =>
        new()
        {
            CatalogType = CatalogType.Normal,
            RootPageId = -1,
            PagesById = ImmutableDictionary<int, CatalogPageSnapshot>.Empty,
            OffersById = ImmutableDictionary<int, CatalogOfferSnapshot>.Empty,
            ProductsById = ImmutableDictionary<int, CatalogProductSnapshot>.Empty,
            PageChildrenIds = ImmutableDictionary<int, ImmutableArray<int>>.Empty,
            PageOfferIds = ImmutableDictionary<int, ImmutableArray<int>>.Empty,
            OfferProductIds = ImmutableDictionary<int, ImmutableArray<int>>.Empty,
        };
}
