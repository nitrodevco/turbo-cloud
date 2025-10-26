using System;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Catalog.Abstractions;
using Turbo.Contracts.Enums.Catalog;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog;

public class CatalogFactory(IServiceProvider sp) : ICatalogFactory
{
    public ICatalog CreateCatalog(CatalogTypeEnum catalogType)
    {
        return ActivatorUtilities.CreateInstance<Catalog>(sp, catalogType);
    }

    public ICatalogPage CreateCatalogRoot()
    {
        return ActivatorUtilities.CreateInstance<CatalogRoot>(sp);
    }

    public ICatalogOffer CreateCatalogOffer(CatalogOfferSnapshot offerSnapshot)
    {
        return ActivatorUtilities.CreateInstance<CatalogOffer>(sp, offerSnapshot);
    }

    public ICatalogPage CreateCatalogPage(CatalogPageSnapshot pageSnapshot)
    {
        return ActivatorUtilities.CreateInstance<CatalogPage>(sp, pageSnapshot);
    }

    public ICatalogProduct CreateCatalogProduct(CatalogProductSnapshot productSnapshot)
    {
        return ActivatorUtilities.CreateInstance<CatalogProduct>(sp, productSnapshot);
    }
}
