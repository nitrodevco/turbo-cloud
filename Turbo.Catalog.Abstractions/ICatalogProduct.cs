using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog.Abstractions;

public interface ICatalogProduct
{
    public CatalogProductSnapshot Snapshot { get; }
    public ICatalogOffer? Offer { get; }
    void SetOffer(ICatalogOffer? offer);
}
