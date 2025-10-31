using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Catalog.Abstractions;
using Turbo.Contracts.Enums.Catalog;
using Turbo.Database.Context;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog;

public sealed class CatalogService(
    ILogger<ICatalogService> logger,
    ICatalogProvider catalogProvider,
    IDbContextFactory<TurboDbContext> dbContextFactory
) : ICatalogService
{
    private readonly ILogger<ICatalogService> _logger = logger;
    private readonly ICatalogProvider _catalogProvider = catalogProvider;

    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly IDictionary<CatalogTypeEnum, ICatalog> _catalogs =
        new ConcurrentDictionary<CatalogTypeEnum, ICatalog>();

    public CatalogSnapshot? GetCatalog(CatalogTypeEnum catalogType)
    {
        var current = _catalogProvider.Current;

        return current;
    }

    public async ValueTask LoadCatalogAsync(CatalogTypeEnum catalogType, CancellationToken ct)
    {
        try
        {
            await _catalogProvider.ReloadAsync(ct).ConfigureAwait(false);

            var current = _catalogProvider.Current;

            _logger.LogInformation(
                "Loaded catalog snapshot: Type={CatalogType}, Pages={PageCount}, Offers={OfferCount}, Products={ProductCount}",
                catalogType,
                current.PagesById.Count,
                current.OffersById.Count,
                current.ProductsById.Count
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
