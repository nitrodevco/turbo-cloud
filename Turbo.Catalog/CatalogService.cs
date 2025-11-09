using Microsoft.Extensions.Logging;
using Turbo.Contracts.Enums.Catalog;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog;

public sealed class CatalogService(
    ILogger<ICatalogService> logger,
    ICatalogProvider<NormalCatalog> catalogProvider
) : ICatalogService
{
    private readonly ILogger<ICatalogService> _logger = logger;
    private readonly ICatalogProvider<NormalCatalog> _catalogProvider = catalogProvider;

    public CatalogSnapshot? GetCatalog(CatalogTypeEnum catalogType)
    {
        if (catalogType != CatalogTypeEnum.Normal)
        {
            _logger.LogWarning("Requested unsupported catalog type: {CatalogType}", catalogType);

            return null;
        }

        return _catalogProvider.Current;
    }
}
