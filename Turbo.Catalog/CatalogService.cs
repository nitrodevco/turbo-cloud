using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Catalog.Tags;

namespace Turbo.Catalog;

public sealed class CatalogService(
    ILogger<ICatalogService> logger,
    ICatalogSnapshotProvider<NormalCatalog> normalCatalogProvider,
    IDbContextFactory<TurboDbContext> dbCtxFactory
) : ICatalogService
{
    private readonly ILogger<ICatalogService> _logger = logger;
    private readonly ICatalogSnapshotProvider<NormalCatalog> _normalCatalogProvider =
        normalCatalogProvider;
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;

    public CatalogSnapshot GetCatalogSnapshot(CatalogType catalogType)
    {
        return catalogType switch
        {
            CatalogType.Normal => _normalCatalogProvider.Current,
            CatalogType.BuildersClub => throw new NotSupportedException(
                $"Catalog type {catalogType} is not supported."
            ),
            _ => throw new NotSupportedException($"Catalog type {catalogType} is not supported."),
        };
    }

    public async Task<UpcomingLtdSnapshot?> GetUpcomingLtdAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        // Find the nearest upcoming active LTD drop
        var now = DateTime.UtcNow;
        var nextSeries = await dbCtx
            .LtdSeries.AsNoTracking()
            .Where(s => s.IsActive && s.StartsAt > now)
            .OrderBy(s => s.StartsAt)
            .FirstOrDefaultAsync(ct);

        if (nextSeries == null)
            return null;

        var catalogSnap = GetCatalogSnapshot(CatalogType.Normal);
        var product = catalogSnap.ProductsById.Values.FirstOrDefault(p =>
            p.LtdSeriesId == nextSeries.Id
        );

        if (product == null)
            return null;

        // Resolve PageId from Offer
        if (!catalogSnap.OffersById.TryGetValue(product.OfferId, out var offer))
            return null;

        return new UpcomingLtdSnapshot
        {
            SecondsUntil = (int)(nextSeries.StartsAt!.Value - now).TotalSeconds,
            PageId = offer.PageId,
            OfferId = offer.Id,
            ClassName = product.ClassName,
        };
    }
}
