using System.Collections.Generic;
using Turbo.Catalog.Abstractions;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog;

public sealed class CatalogOffer(CatalogOfferSnapshot offerSnapshot) : ICatalogOffer
{
    public CatalogOfferSnapshot Snapshot => offerSnapshot;
    public ICatalogPage? Page { get; private set; }
    public IDictionary<int, ICatalogProduct> Products { get; } =
        new Dictionary<int, ICatalogProduct>();

    public void SetPage(ICatalogPage? page)
    {
        if (Page == page)
            return;

        Page = page;

        Page?.AddOffer(this);
    }

    public void AddProduct(ICatalogProduct product)
    {
        if (Products.ContainsKey(product.Snapshot.Id))
            return;

        Products.Add(product.Snapshot.Id, product);

        product.SetOffer(this);
    }
}
