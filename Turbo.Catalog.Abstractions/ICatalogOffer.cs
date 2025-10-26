using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog.Abstractions;

public interface ICatalogOffer
{
    CatalogOfferSnapshot Snapshot { get; }
    ICatalogPage? Page { get; }
    void SetPage(ICatalogPage? page);
    void AddProduct(ICatalogProduct product);
}
