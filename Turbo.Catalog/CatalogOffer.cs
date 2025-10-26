using Turbo.Catalog.Abstractions;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog;

public sealed class CatalogOffer(CatalogOfferSnapshot offerSnapshot) : ICatalogOffer
{
    public CatalogOfferSnapshot Snapshot => offerSnapshot;
    public ICatalogPage? Page { get; private set; }

    public void SetPage(ICatalogPage? page)
    {
        Page = page;

        Page?.AddOffer(this);
    }

    public void AddProduct(ICatalogProduct product)
    {
        // Implementation for adding a product to the offer
    }
}
