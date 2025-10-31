using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog.Abstractions;

public interface ICatalogOffer
{
    public CatalogOfferSnapshot Snapshot { get; }
    public ICatalogPage? Page { get; }
    void SetPage(ICatalogPage? page);
    void AddProduct(ICatalogProduct product);
}
