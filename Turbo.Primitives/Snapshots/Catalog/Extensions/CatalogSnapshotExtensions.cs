using System.Collections.Immutable;

namespace Turbo.Primitives.Snapshots.Catalog.Extensions;

public static class CatalogSnapshotExtensions
{
    public static CatalogPageSnapshot GetPageById(this CatalogSnapshot snapshot, int pageId) =>
        snapshot.PagesById[pageId];

    public static CatalogOfferSnapshot GetOfferById(this CatalogSnapshot snapshot, int offerId) =>
        snapshot.OffersById[offerId];

    public static CatalogProductSnapshot GetProductById(
        this CatalogSnapshot snapshot,
        int productId
    ) => snapshot.ProductsById[productId];

    public static ImmutableArray<int> GetOfferIdsByPageId(
        this CatalogSnapshot snapshot,
        int pageId
    ) => snapshot.PageOffers.TryGetValue(pageId, out var offerIds) ? offerIds : [];

    public static ImmutableArray<int> GetChildPageIdsByPageId(
        this CatalogSnapshot snapshot,
        int pageId
    ) => snapshot.PageChildren.TryGetValue(pageId, out var pageIds) ? pageIds : [];

    public static ImmutableArray<int> GetProductIdsByOfferId(
        this CatalogSnapshot snapshot,
        int offerId
    ) => snapshot.OfferProducts.TryGetValue(offerId, out var productIds) ? productIds : [];
}
