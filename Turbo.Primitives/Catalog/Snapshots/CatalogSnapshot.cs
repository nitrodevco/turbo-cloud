using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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

    /// <summary>
    /// The product that sells each limited series, by series id. The LTD raffle asks on every
    /// entry and draw; without this it scanned every product.
    /// </summary>
    [Id(8)]
    public required IImmutableDictionary<int, int> ProductIdByLtdSeriesId { get; init; }

    /// <summary>
    /// The featured items of the front page, in their order, expired or not: an item's end is
    /// checked when a page is sent (<see cref="FrontPageItemsFor"/>). Only the normal catalog
    /// has any.
    /// </summary>
    [Id(9)]
    public ImmutableArray<CatalogFrontPageItemSnapshot> FrontPageItems { get; init; } = [];

    /// <summary>
    /// Whether any page asks more than being a player (a rank or a node,
    /// <see cref="CatalogPageAccess"/>). Without one, every player is sent this catalog as it is.
    /// </summary>
    [Id(10)]
    public bool HasGatedPages { get; init; }

    /// <summary>
    /// The featured items a page is sent with at <paramref name="now"/>: none unless its layout
    /// draws them, and of those only the ones that have not ended, each counting down to its
    /// end. The client keeps the items of the last page that brought any, so every other page
    /// sends none and leaves them be.
    /// </summary>
    public ImmutableArray<CatalogFrontPageItemSnapshot> FrontPageItemsFor(
        CatalogPageSnapshot page,
        DateTime now
    )
    {
        if (FrontPageItems.IsDefaultOrEmpty || !CatalogPageLayouts.ShowsFeaturedItems(page.Layout))
            return [];

        var items = ImmutableArray.CreateBuilder<CatalogFrontPageItemSnapshot>();

        foreach (var item in FrontPageItems)
        {
            if (item.ExpiresAt is not { } ends)
            {
                items.Add(item with { ExpiresInSeconds = 0 });

                continue;
            }

            if (ends <= now)
                continue;

            // Rounded up, so an item with under a second left still counts down rather than
            // being sent as one that never ends.
            var seconds = Math.Ceiling((ends - now).TotalSeconds);

            items.Add(item with { ExpiresInSeconds = (int)Math.Min(seconds, int.MaxValue) });
        }

        return items.ToImmutable();
    }

    /// <summary>The product that sells this limited series, if the catalog has one.</summary>
    public bool TryGetLtdProduct(
        int seriesId,
        [NotNullWhen(true)] out CatalogProductSnapshot? product
    )
    {
        product = null;

        return ProductIdByLtdSeriesId.TryGetValue(seriesId, out var productId)
            && ProductsById.TryGetValue(productId, out product);
    }

    /// <summary>
    /// This catalog with a limited series showing how many are left now: the product that sells
    /// it, and the offer listing that product, both carry the count the page and the product
    /// offer send. The same snapshot when no product sells the series.
    /// </summary>
    public CatalogSnapshot WithLtdRemaining(int seriesId, int remaining)
    {
        if (!TryGetLtdProduct(seriesId, out var product) || product.UniqueRemaining == remaining)
            return this;

        var updated = product with { UniqueRemaining = remaining };
        var offersById = OffersById;

        if (OffersById.TryGetValue(product.OfferId, out var offer))
        {
            offersById = OffersById.SetItem(
                offer.Id,
                offer with
                {
                    Products = [.. offer.Products.Select(p => p.Id == product.Id ? updated : p)],
                }
            );
        }

        return this with
        {
            ProductsById = ProductsById.SetItem(product.Id, updated),
            OffersById = offersById,
        };
    }

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
            ProductIdByLtdSeriesId = ImmutableDictionary<int, int>.Empty,
            FrontPageItems = [],
        };
}
