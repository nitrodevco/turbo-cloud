using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Primitives.Catalog;

public interface ICatalogService
{
    public CatalogSnapshot? GetCatalog(CatalogType catalogType);
}
