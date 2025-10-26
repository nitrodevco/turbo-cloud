using Turbo.Catalog.Abstractions;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog;

public sealed class CatalogProduct(CatalogProductSnapshot productSnapshot) : ICatalogProduct
{
    public CatalogProductSnapshot Snapshot => productSnapshot;
    public ICatalogOffer? Offer { get; private set; }

    public void SetOffer(ICatalogOffer? offer)
    {
        if (Offer == offer)
            return;

        Offer = offer;

        Offer?.AddProduct(this);
    }
}
