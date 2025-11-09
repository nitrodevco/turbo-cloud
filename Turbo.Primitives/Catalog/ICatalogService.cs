using Turbo.Contracts.Enums.Catalog;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Primitives.Catalog;

public interface ICatalogService
{
    public CatalogSnapshot? GetCatalog(CatalogTypeEnum catalogType);
}
