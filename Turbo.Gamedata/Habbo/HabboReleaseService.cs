using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Database.Entities.Gamedata;
using Turbo.Database.Extensions;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Texts;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Gamedata.Habbo;

/// <summary>
/// Asks Habbo (<see cref="GamedataConfig.HabboDomain"/>) for its external variables and furniture
/// data, and keeps the furniture data when it is new: one row per furniture data, by its hash,
/// with the revision its <c>flash.client.url</c> named when it was last seen. Nothing is taken in
/// here; staff review an import first.
/// </summary>
internal sealed class HabboReleaseService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config,
    HabboGamedataClient habbo,
    TimeProvider time,
    ILogger<HabboReleaseService> logger
) : IHabboReleaseService
{
    private const string CLIENT_URL_VARIABLE = "flash.client.url";
    private const string REVISION_PREFIX = "flash-assets-";

    private readonly GamedataConfig _config = config.Value;

    public async Task<HabboCheckResult> CheckAsync(CancellationToken ct)
    {
        var domain = _config.HabboDomain;
        var variables = await habbo.GetExternalVariablesAsync(domain, ct).ConfigureAwait(false);
        var revision =
            RevisionOf(variables)
            ?? throw new HttpRequestException(
                $"habbo.{domain}'s external variables name no {CLIENT_URL_VARIABLE}, so its revision is unknown."
            );
        var data = await habbo.GetFurnitureDataAsync(domain, ct).ConfigureAwait(false);
        var texts = await habbo.GetExternalTextsAsync(domain, ct).ConfigureAwait(false);
        var products = await habbo.GetProductDataAsync(domain, ct).ConfigureAwait(false);
        var figures = await habbo.GetFigureDataAsync(domain, ct).ConfigureAwait(false);
        var now = time.GetUtcNow().UtcDateTime;
        var (release, isNew) = await KeepReleaseAsync(domain, revision, data, now, ct)
            .ConfigureAwait(false);
        var (version, textsAreNew) = await KeepTextsAsync(domain, texts, now, ct)
            .ConfigureAwait(false);
        var (productVersion, productsAreNew) = await KeepProductsAsync(domain, products, now, ct)
            .ConfigureAwait(false);
        var (figureVersion, figuresAreNew) = await KeepFiguresAsync(domain, figures, now, ct)
            .ConfigureAwait(false);

        return new HabboCheckResult
        {
            Release = release,
            IsNew = isNew,
            Texts = version,
            TextsAreNew = textsAreNew,
            Products = productVersion,
            ProductsAreNew = productsAreNew,
            Figures = figureVersion,
            FiguresAreNew = figuresAreNew,
        };
    }

    public async Task<HabboTextVersionSnapshot?> GetLatestTextsAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var latest = await dbCtx
            .HabboTextVersions.AsNoTracking()
            .Where(x => x.Domain == _config.HabboDomain)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return latest?.ToSnapshot();
    }

    public async Task<HabboProductVersionSnapshot?> GetLatestProductsAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var latest = await dbCtx
            .HabboProductVersions.AsNoTracking()
            .Where(x => x.Domain == _config.HabboDomain)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return latest?.ToSnapshot();
    }

    public async Task<HabboFigureVersionSnapshot?> GetLatestFiguresAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var latest = await dbCtx
            .HabboFigureVersions.AsNoTracking()
            .Where(x => x.Domain == _config.HabboDomain)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return latest?.ToSnapshot();
    }

    /// <summary>The version of this figure data: the one kept before, or a new one.</summary>
    private async Task<(HabboFigureVersionSnapshot Version, bool IsNew)> KeepFiguresAsync(
        string domain,
        byte[] figures,
        DateTime now,
        CancellationToken ct
    )
    {
        var hash = GamedataBytes.Hash(figures);
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var known = await dbCtx
            .HabboFigureVersions.FirstOrDefaultAsync(x => x.Domain == domain && x.Hash == hash, ct)
            .ConfigureAwait(false);

        if (known is not null)
        {
            known.CheckedAt = now;

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            return (known.ToSnapshot(), false);
        }

        List<(
            Primitives.Gamedata.Enums.FigureRecordKind Kind,
            System.Text.Json.Nodes.JsonObject Record
        )> records;

        try
        {
            records = Figures.FigureDataFile.Parse(figures);
        }
        catch (FormatException ex)
        {
            throw new HttpRequestException($"habbo.{domain}'s figure data doesn't read.", ex);
        }

        var version = new HabboFigureVersionEntity
        {
            Domain = domain,
            Hash = hash,
            Content = GamedataBytes.Compress(figures),
            SetCount = records.Count(x => x.Kind == Primitives.Gamedata.Enums.FigureRecordKind.Set),
            ColorCount = records.Count(x =>
                x.Kind == Primitives.Gamedata.Enums.FigureRecordKind.Color
            ),
            CheckedAt = now,
        };

        dbCtx.HabboFigureVersions.Add(version);

        try
        {
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Another silo's check kept the same figure data a moment before this one.
            var retryCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var retryCtxScope = retryCtx.ConfigureAwait(false);

            var kept = await retryCtx
                .HabboFigureVersions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Domain == domain && x.Hash == hash, ct)
                .ConfigureAwait(false);

            if (kept is not null)
                return (kept.ToSnapshot(), false);

            logger.LogError(ex, "Failed to keep habbo.{Domain}'s figure data {Hash}", domain, hash);

            throw;
        }

        logger.LogInformation(
            "habbo.{Domain} serves new figure data {Hash}: {Sets} pieces, {Colors} colours",
            domain,
            hash,
            version.SetCount,
            version.ColorCount
        );

        return (version.ToSnapshot(), true);
    }

    /// <summary>The version of this product data: the one kept before, or a new one.</summary>
    private async Task<(HabboProductVersionSnapshot Version, bool IsNew)> KeepProductsAsync(
        string domain,
        byte[] products,
        DateTime now,
        CancellationToken ct
    )
    {
        var hash = GamedataBytes.Hash(products);
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var known = await dbCtx
            .HabboProductVersions.FirstOrDefaultAsync(x => x.Domain == domain && x.Hash == hash, ct)
            .ConfigureAwait(false);

        if (known is not null)
        {
            known.CheckedAt = now;

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            return (known.ToSnapshot(), false);
        }

        var version = new HabboProductVersionEntity
        {
            Domain = domain,
            Hash = hash,
            Content = GamedataBytes.Compress(products),
            ProductCount = Products.ProductDataFile.Parse(products).Count,
            CheckedAt = now,
        };

        dbCtx.HabboProductVersions.Add(version);

        try
        {
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Another silo's check kept the same product data a moment before this one.
            var retryCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var retryCtxScope = retryCtx.ConfigureAwait(false);

            var kept = await retryCtx
                .HabboProductVersions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Domain == domain && x.Hash == hash, ct)
                .ConfigureAwait(false);

            if (kept is not null)
                return (kept.ToSnapshot(), false);

            logger.LogError(
                ex,
                "Failed to keep habbo.{Domain}'s product data {Hash}",
                domain,
                hash
            );

            throw;
        }

        logger.LogInformation(
            "habbo.{Domain} serves new product data {Hash}: {Count} products",
            domain,
            hash,
            version.ProductCount
        );

        return (version.ToSnapshot(), true);
    }

    /// <summary>The release of this furniture data: the one kept before, or a new one.</summary>
    private async Task<(HabboReleaseSnapshot Release, bool IsNew)> KeepReleaseAsync(
        string domain,
        string revision,
        byte[] data,
        DateTime now,
        CancellationToken ct
    )
    {
        var hash = GamedataBytes.Hash(data);
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        if (await FindAsync(dbCtx, domain, hash, ct).ConfigureAwait(false) is { } known)
        {
            known.CheckedAt = now;
            known.Revision = revision;

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            return (known.ToSnapshot(), false);
        }

        var release = new HabboReleaseEntity
        {
            Domain = domain,
            Revision = revision,
            FurnitureDataHash = hash,
            FurnitureData = GamedataBytes.Compress(data),
            FurnitureCount = CountItems(data),
            CheckedAt = now,
        };

        dbCtx.HabboReleases.Add(release);

        try
        {
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Another silo's check kept the same data a moment before this one.
            var retryCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var retryCtxScope = retryCtx.ConfigureAwait(false);

            if (await FindAsync(retryCtx, domain, hash, ct).ConfigureAwait(false) is { } kept)
                return (kept.ToSnapshot(), false);

            logger.LogError(
                ex,
                "Failed to keep habbo.{Domain}'s furniture data {Hash}",
                domain,
                hash
            );

            throw;
        }

        logger.LogInformation(
            "habbo.{Domain} serves new furniture data {Hash}: revision {Revision}, {Count} items",
            domain,
            hash,
            revision,
            release.FurnitureCount
        );

        return (release.ToSnapshot(), true);
    }

    /// <summary>The version of these texts: the one kept before, or a new one.</summary>
    private async Task<(HabboTextVersionSnapshot Version, bool IsNew)> KeepTextsAsync(
        string domain,
        byte[] texts,
        DateTime now,
        CancellationToken ct
    )
    {
        var hash = GamedataBytes.Hash(texts);
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var known = await dbCtx
            .HabboTextVersions.FirstOrDefaultAsync(x => x.Domain == domain && x.Hash == hash, ct)
            .ConfigureAwait(false);

        if (known is not null)
        {
            known.CheckedAt = now;

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            return (known.ToSnapshot(), false);
        }

        var version = new HabboTextVersionEntity
        {
            Domain = domain,
            Hash = hash,
            Content = GamedataBytes.Compress(texts),
            TextCount = ExternalTextsFile.Parse(Encoding.UTF8.GetString(texts)).Count,
            CheckedAt = now,
        };

        dbCtx.HabboTextVersions.Add(version);

        try
        {
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Another silo's check kept the same texts a moment before this one.
            var retryCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var retryCtxScope = retryCtx.ConfigureAwait(false);

            var kept = await retryCtx
                .HabboTextVersions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Domain == domain && x.Hash == hash, ct)
                .ConfigureAwait(false);

            if (kept is not null)
                return (kept.ToSnapshot(), false);

            logger.LogError(ex, "Failed to keep habbo.{Domain}'s texts {Hash}", domain, hash);

            throw;
        }

        logger.LogInformation(
            "habbo.{Domain} serves new external texts {Hash}: {Count} texts",
            domain,
            hash,
            version.TextCount
        );

        return (version.ToSnapshot(), true);
    }

    public async Task<HabboReleaseSnapshot?> GetLatestAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var latest = await dbCtx
            .HabboReleases.AsNoTracking()
            .Where(x => x.Domain == _config.HabboDomain)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return latest?.ToSnapshot();
    }

    public async Task<HabboReleaseSnapshot?> GetAsync(int id, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var release = await dbCtx
            .HabboReleases.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        return release?.ToSnapshot();
    }

    /// <summary>The <c>&lt;revision&gt;</c> of <c>flash.client.url</c>'s <c>flash-assets-&lt;revision&gt;</c>.</summary>
    internal static string? RevisionOf(IReadOnlyDictionary<string, string> variables)
    {
        if (!variables.TryGetValue(CLIENT_URL_VARIABLE, out var url))
            return null;

        var start = url.IndexOf(REVISION_PREFIX, StringComparison.Ordinal);

        if (start < 0)
            return null;

        start += REVISION_PREFIX.Length;

        var end = url.IndexOf('/', start);
        var revision = end < 0 ? url[start..] : url[start..end];

        return revision.Length > 0 ? revision : null;
    }

    private static Task<HabboReleaseEntity?> FindAsync(
        TurboDbContext dbCtx,
        string domain,
        string hash,
        CancellationToken ct
    ) =>
        dbCtx.HabboReleases.FirstOrDefaultAsync(
            x => x.Domain == domain && x.FurnitureDataHash == hash,
            ct
        );

    private static int CountItems(byte[] data)
    {
        using var document = JsonDocument.Parse(data);
        var count = 0;

        foreach (var list in new[] { "roomitemtypes", "wallitemtypes" })
            if (
                document.RootElement.TryGetProperty(list, out var types)
                && types.TryGetProperty("furnitype", out var items)
                && items.ValueKind == JsonValueKind.Array
            )
                count += items.GetArrayLength();

        return count;
    }
}
