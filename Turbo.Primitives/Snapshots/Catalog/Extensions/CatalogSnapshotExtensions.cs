using System.Collections.Immutable;

namespace Turbo.Primitives.Snapshots.Catalog.Extensions;

public static class CatalogSnapshotExtensions
{
    public static CatalogPageSnapshot? GetPageById(this CatalogSnapshot snapshot, int pageId)
    {
        return snapshot.PagesById.TryGetValue(pageId, out var page) ? page : null;
    }

    public static CatalogOfferSnapshot? GetOfferById(this CatalogSnapshot snapshot, int offerId)
    {
        return snapshot.OffersById.TryGetValue(offerId, out var offer) ? offer : null;
    }

    public static CatalogProductSnapshot? GetProductById(
        this CatalogSnapshot snapshot,
        int productId
    )
    {
        return snapshot.ProductsById.TryGetValue(productId, out var product) ? product : null;
    }

    public static ImmutableArray<int> GetOfferIdsByPageId(this CatalogSnapshot snapshot, int pageId)
    {
        return snapshot.PageOffers.TryGetValue(pageId, out var offers) ? offers : [];
    }

    public static ImmutableArray<int> GetChildPageIdsByPageId(
        this CatalogSnapshot snapshot,
        int pageId
    )
    {
        return snapshot.PageChildren.TryGetValue(pageId, out var children) ? children : [];
    }

    public static ImmutableArray<int> GetProductIdsByOfferId(
        this CatalogSnapshot snapshot,
        int offerId
    )
    {
        return snapshot.OfferProducts.TryGetValue(offerId, out var products) ? products : [];
    }
}
