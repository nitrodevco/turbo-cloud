using Turbo.Contracts.Enums.Catalog;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog.Abstractions;

public interface ICatalogService
{
    public CatalogSnapshot? GetCatalog(CatalogTypeEnum catalogType);
}
