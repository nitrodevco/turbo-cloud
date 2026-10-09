using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Catalog.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Extensions;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Snapshots;

namespace Turbo.Catalog.Reception;

/// <summary>
/// The expiring pages (<see cref="IExpiringPageService"/>), kept in <c>catalog_page_expiries</c>.
/// Every expiry is read at once and kept for <see cref="ReceptionConfig.CacheSeconds"/>; the one
/// shown is picked from that by the time asked, so a page that runs out gives way on time.
/// </summary>
public sealed class ExpiringPageService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<CatalogConfig> config,
    TimeProvider time,
    ILogger<ExpiringPageService> logger
) : IExpiringPageService
{
    private readonly ReceptionConfig _config = config.Value.Reception;

    private (DateTimeOffset ReadAt, ImmutableArray<CatalogPageExpirySnapshot> Pages)? _read;

    public async Task<CatalogPageExpirySnapshot?> GetEarliestAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var read = _read;

        if (read is null || now - read.Value.ReadAt >= TimeSpan.FromSeconds(_config.CacheSeconds))
        {
            read = (now, await ListAsync(ct).ConfigureAwait(false));
            _read = read;
        }

        return read.Value.Pages.Where(x => x.ExpiresAt > now.UtcDateTime).MinBy(x => x.ExpiresAt);
    }

    public async Task<ImmutableArray<CatalogPageExpirySnapshot>> ListAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .CatalogPageExpiries.AsNoTracking()
            .OrderBy(x => x.ExpiresAt)
            .Select(x => new { Expiry = x, PageName = x.CatalogPageEntity!.Name })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. rows.Select(x => x.Expiry.ToSnapshot(x.PageName ?? string.Empty))];
    }

    public async Task<CatalogPageExpirySnapshot> SaveAsync(
        int pageId,
        DateTime expiresAt,
        string image,
        CancellationToken ct
    )
    {
        image = image.Trim();

        if (image.Length > CatalogPageExpiryEntity.IMAGE_MAX_LENGTH)
            throw new ArgumentException("The image is too long.", nameof(image));

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var page =
            await dbCtx
                .CatalogPages.AsNoTracking()
                .Where(x => x.Id == pageId)
                .Select(x => new { x.Name })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false)
            ?? throw new ArgumentException($"There is no catalog page {pageId}.", nameof(pageId));

        if (string.IsNullOrWhiteSpace(page.Name))
            throw new ArgumentException(
                "The page needs a name: the client opens it, and finds its texts and teaser, by it.",
                nameof(pageId)
            );

        var row = await dbCtx
            .CatalogPageExpiries.FirstOrDefaultAsync(x => x.CatalogPageEntityId == pageId, ct)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new CatalogPageExpiryEntity { CatalogPageEntityId = pageId, Image = image };
            dbCtx.CatalogPageExpiries.Add(row);
        }

        row.ExpiresAt = DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc);
        row.Image = image;

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        _read = null;
        logger.LogInformation(
            "Catalog page {PageId} ({Name}) expires at {ExpiresAt}",
            pageId,
            page.Name,
            row.ExpiresAt
        );

        return row.ToSnapshot(page.Name);
    }

    public async Task<bool> DeleteAsync(int pageId, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .CatalogPageExpiries.FirstOrDefaultAsync(x => x.CatalogPageEntityId == pageId, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        dbCtx.CatalogPageExpiries.Remove(row);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        _read = null;

        return true;
    }
}
