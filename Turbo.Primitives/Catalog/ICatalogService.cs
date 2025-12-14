using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;

namespace Turbo.Primitives.Catalog;

public interface ICatalogService
{
    public CatalogSnapshot GetCatalogSnapshot(CatalogType catalogType);
}
