using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog.Abstractions;

public interface ICatalogProduct
{
    CatalogProductSnapshot Snapshot { get; }
    ICatalogOffer? Offer { get; }
    void SetOffer(ICatalogOffer? offer);
}
