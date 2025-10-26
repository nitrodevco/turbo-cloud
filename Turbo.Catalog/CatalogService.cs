using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Catalog.Abstractions;
using Turbo.Contracts.Enums.Catalog;

namespace Turbo.Catalog;

public sealed class CatalogService(ILogger<ICatalogService> logger, ICatalogFactory catalogFactory)
    : ICatalogService
{
    private readonly ILogger<ICatalogService> _logger = logger;
    private readonly ICatalogFactory _catalogFactory = catalogFactory;
    private readonly IDictionary<CatalogTypeEnum, ICatalog> _catalogs =
        new ConcurrentDictionary<CatalogTypeEnum, ICatalog>();

    public async Task LoadCatalogAsync(CatalogTypeEnum catalogType, CancellationToken ct)
    {
        if (_catalogs.ContainsKey(catalogType))
            return;

        var catalog = _catalogFactory.CreateCatalog(catalogType);

        await catalog.LoadCatalogAsync(ct).ConfigureAwait(false);
    }
}
