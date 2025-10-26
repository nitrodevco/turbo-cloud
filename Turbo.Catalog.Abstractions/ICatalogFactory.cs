using Turbo.Contracts.Enums.Catalog;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog.Abstractions;

public interface ICatalogFactory
{
    public ICatalog CreateCatalog(CatalogTypeEnum catalogType);
    public ICatalogPage CreateCatalogRoot();
    public ICatalogOffer CreateCatalogOffer(CatalogOfferSnapshot offerSnapshot);
    public ICatalogPage CreateCatalogPage(CatalogPageSnapshot pageSnapshot);
    public ICatalogProduct CreateCatalogProduct(CatalogProductSnapshot productSnapshot);
}
