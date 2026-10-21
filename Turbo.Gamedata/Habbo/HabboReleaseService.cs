using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Database.Entities;
using Turbo.Database.Entities.Gamedata;
using Turbo.Database.Extensions;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Figures;
using Turbo.Gamedata.Products;
using Turbo.Gamedata.Texts;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Gamedata.Habbo;

/// <summary>
/// Asks Habbo (<see cref="GamedataConfig.HabboDomain"/>) for its external variables and furniture
/// data, and keeps the furniture data when it is new: one row per furniture data, by its hash,
/// with the revision its <c>flash.client.url</c> named when it was last seen. Its texts, product
/// data, figure data and the external variables themselves are kept the same way, by hash.
/// Nothing is taken in here; staff review an import first.
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

    // The names Habbo's /gamedata/hashes lists its files under.
    private const string FURNITURE_DATA = "furnidata";
    private const string PRODUCT_DATA = "productdata";
    private const string EXTERNAL_TEXTS = "external_texts";
    private const string EXTERNAL_VARIABLES = "external_variables";
    private const string FIGURE_DATA = "figurepartlist";

    private readonly GamedataConfig _config = config.Value;

    public async Task<HabboCheckResult> CheckAsync(CancellationToken ct)
    {
        var domain = _config.HabboDomain;
        var hashes = await HashesAsync(domain, ct).ConfigureAwait(false);
        var variablesFile = await habbo
            .GetExternalVariablesFileAsync(domain, ct)
            .ConfigureAwait(false);
        var variables = HabboGamedataClient.ParseVariables(variablesFile);
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
        var (release, isNew) = await KeepReleaseAsync(
                domain,
                revision,
                data,
                hashes.GetValueOrDefault(FURNITURE_DATA),
                now,
                ct
            )
            .ConfigureAwait(false);
        var (version, textsAreNew) = await KeepTextsAsync(
                domain,
                texts,
                hashes.GetValueOrDefault(EXTERNAL_TEXTS),
                now,
                ct
            )
            .ConfigureAwait(false);
        var (productVersion, productsAreNew) = await KeepProductsAsync(
                domain,
                products,
                hashes.GetValueOrDefault(PRODUCT_DATA),
                now,
                ct
            )
            .ConfigureAwait(false);
        var (figureVersion, figuresAreNew) = await KeepFiguresAsync(
                domain,
                figures,
                hashes.GetValueOrDefault(FIGURE_DATA),
                now,
                ct
            )
            .ConfigureAwait(false);
        var (variableVersion, variablesAreNew) = await KeepVariablesAsync(
                domain,
                variablesFile,
                variables.Count,
                now,
                ct,
                hashes.GetValueOrDefault(EXTERNAL_VARIABLES)
            )
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
            Variables = variableVersion,
            VariablesAreNew = variablesAreNew,
        };
    }

    /// <summary>
    /// Habbo's <c>/gamedata/hashes</c>, read before the files so each version keeps the hash Habbo
    /// listed it under; empty when it could not be read, which a check goes on without.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> HashesAsync(
        string domain,
        CancellationToken ct
    )
    {
        try
        {
            return await habbo.GetHashesAsync(domain, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "habbo.{Domain}'s gamedata hashes could not be read: {Message}",
                domain,
                ex.Message
            );

            return new Dictionary<string, string>();
        }
    }

    public async Task<HabboReleaseSnapshot?> GetLatestAsync(CancellationToken ct) =>
        (await LatestAsync<HabboReleaseEntity>(ct).ConfigureAwait(false))?.ToSnapshot();

    public async Task<HabboTextVersionSnapshot?> GetLatestTextsAsync(CancellationToken ct) =>
        (await LatestAsync<HabboTextVersionEntity>(ct).ConfigureAwait(false))?.ToSnapshot();

    public async Task<HabboProductVersionSnapshot?> GetLatestProductsAsync(CancellationToken ct) =>
        (await LatestAsync<HabboProductVersionEntity>(ct).ConfigureAwait(false))?.ToSnapshot();

    public async Task<HabboFigureVersionSnapshot?> GetLatestFiguresAsync(CancellationToken ct) =>
        (await LatestAsync<HabboFigureVersionEntity>(ct).ConfigureAwait(false))?.ToSnapshot();

    public async Task<HabboVariableVersionSnapshot?> GetLatestVariablesAsync(
        CancellationToken ct
    ) => (await LatestAsync<HabboVariableVersionEntity>(ct).ConfigureAwait(false))?.ToSnapshot();

    private async Task<T?> LatestAsync<T>(CancellationToken ct)
        where T : TurboEntity, IHabboVersionEntity
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        return await dbCtx
            .Set<T>()
            .AsNoTracking()
            .Latest(_config.HabboDomain)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>The version of these external variables: the one kept before, or a new one.</summary>
    internal async Task<(HabboVariableVersionSnapshot Version, bool IsNew)> KeepVariablesAsync(
        string domain,
        byte[] variables,
        int count,
        DateTime now,
        CancellationToken ct,
        string? habboHash = null
    )
    {
        var hash = GamedataBytes.Hash(variables);
        var (version, isNew) = await KeepAsync<HabboVariableVersionEntity>(
                "external variables",
                domain,
                hash,
                x => x.Domain == domain && x.Hash == hash,
                () =>
                    new HabboVariableVersionEntity
                    {
                        Domain = domain,
                        Hash = hash,
                        Content = GamedataBytes.Compress(variables),
                        VariableCount = count,
                        CheckedAt = now,
                        HabboHash = habboHash,
                    },
                habboHash,
                now,
                ct
            )
            .ConfigureAwait(false);

        if (isNew)
            logger.LogInformation(
                "habbo.{Domain} serves new external variables {Hash}: {Count} variables",
                domain,
                hash,
                count
            );

        return (version.ToSnapshot(), isNew);
    }

    /// <summary>The version of this figure data: the one kept before, or a new one.</summary>
    private async Task<(HabboFigureVersionSnapshot Version, bool IsNew)> KeepFiguresAsync(
        string domain,
        byte[] figures,
        string? habboHash,
        DateTime now,
        CancellationToken ct
    )
    {
        var hash = GamedataBytes.Hash(figures);
        var (version, isNew) = await KeepAsync<HabboFigureVersionEntity>(
                "figure data",
                domain,
                hash,
                x => x.Domain == domain && x.Hash == hash,
                () =>
                {
                    List<(FigureRecordKind Kind, JsonObject Record)> records;

                    try
                    {
                        records = FigureDataFile.Parse(figures);
                    }
                    catch (FormatException ex)
                    {
                        throw new HttpRequestException(
                            $"habbo.{domain}'s figure data doesn't read.",
                            ex
                        );
                    }

                    return new HabboFigureVersionEntity
                    {
                        Domain = domain,
                        Hash = hash,
                        Content = GamedataBytes.Compress(figures),
                        SetCount = records.Count(x => x.Kind == FigureRecordKind.Set),
                        ColorCount = records.Count(x => x.Kind == FigureRecordKind.Color),
                        CheckedAt = now,
                        HabboHash = habboHash,
                    };
                },
                habboHash,
                now,
                ct
            )
            .ConfigureAwait(false);

        if (isNew)
            logger.LogInformation(
                "habbo.{Domain} serves new figure data {Hash}: {Sets} pieces, {Colors} colours",
                domain,
                hash,
                version.SetCount,
                version.ColorCount
            );

        return (version.ToSnapshot(), isNew);
    }

    /// <summary>The version of this product data: the one kept before, or a new one.</summary>
    private async Task<(HabboProductVersionSnapshot Version, bool IsNew)> KeepProductsAsync(
        string domain,
        byte[] products,
        string? habboHash,
        DateTime now,
        CancellationToken ct
    )
    {
        var hash = GamedataBytes.Hash(products);
        var (version, isNew) = await KeepAsync<HabboProductVersionEntity>(
                "product data",
                domain,
                hash,
                x => x.Domain == domain && x.Hash == hash,
                () =>
                    new HabboProductVersionEntity
                    {
                        Domain = domain,
                        Hash = hash,
                        Content = GamedataBytes.Compress(products),
                        ProductCount = ProductDataFile.Parse(products).Count,
                        CheckedAt = now,
                        HabboHash = habboHash,
                    },
                habboHash,
                now,
                ct
            )
            .ConfigureAwait(false);

        if (isNew)
            logger.LogInformation(
                "habbo.{Domain} serves new product data {Hash}: {Count} products",
                domain,
                hash,
                version.ProductCount
            );

        return (version.ToSnapshot(), isNew);
    }

    /// <summary>The release of this furniture data: the one kept before, or a new one.</summary>
    private async Task<(HabboReleaseSnapshot Release, bool IsNew)> KeepReleaseAsync(
        string domain,
        string revision,
        byte[] data,
        string? habboHash,
        DateTime now,
        CancellationToken ct
    )
    {
        var hash = GamedataBytes.Hash(data);
        var (release, isNew) = await KeepAsync<HabboReleaseEntity>(
                "furniture data",
                domain,
                hash,
                x => x.Domain == domain && x.FurnitureDataHash == hash,
                () =>
                    new HabboReleaseEntity
                    {
                        Domain = domain,
                        Revision = revision,
                        FurnitureDataHash = hash,
                        FurnitureData = GamedataBytes.Compress(data),
                        FurnitureCount = CountItems(data),
                        CheckedAt = now,
                        HabboHash = habboHash,
                    },
                habboHash,
                now,
                ct,
                // A release found again names the revision Habbo serves it under now.
                known => known.Revision = revision
            )
            .ConfigureAwait(false);

        if (isNew)
            logger.LogInformation(
                "habbo.{Domain} serves new furniture data {Hash}: revision {Revision}, {Count} items",
                domain,
                hash,
                revision,
                release.FurnitureCount
            );

        return (release.ToSnapshot(), isNew);
    }

    /// <summary>The version of these texts: the one kept before, or a new one.</summary>
    private async Task<(HabboTextVersionSnapshot Version, bool IsNew)> KeepTextsAsync(
        string domain,
        byte[] texts,
        string? habboHash,
        DateTime now,
        CancellationToken ct
    )
    {
        var hash = GamedataBytes.Hash(texts);
        var (version, isNew) = await KeepAsync<HabboTextVersionEntity>(
                "texts",
                domain,
                hash,
                x => x.Domain == domain && x.Hash == hash,
                () =>
                    new HabboTextVersionEntity
                    {
                        Domain = domain,
                        Hash = hash,
                        Content = GamedataBytes.Compress(texts),
                        TextCount = ExternalTextsFile.Parse(Encoding.UTF8.GetString(texts)).Count,
                        CheckedAt = now,
                        HabboHash = habboHash,
                    },
                habboHash,
                now,
                ct
            )
            .ConfigureAwait(false);

        if (isNew)
            logger.LogInformation(
                "habbo.{Domain} serves new external texts {Hash}: {Count} texts",
                domain,
                hash,
                version.TextCount
            );

        return (version.ToSnapshot(), isNew);
    }

    /// <summary>
    /// The version of a file of Habbo's that <paramref name="same"/> finds, seen again
    /// <paramref name="now"/>; when there is none, the one <paramref name="create"/> makes, kept.
    /// When another silo's check kept the same file a moment before this one, that one.
    /// </summary>
    /// <param name="what">What the file is called in the log.</param>
    /// <param name="seen">What else a version found again takes from this check.</param>
    private async Task<(T Version, bool IsNew)> KeepAsync<T>(
        string what,
        string domain,
        string hash,
        Expression<Func<T, bool>> same,
        Func<T> create,
        string? habboHash,
        DateTime now,
        CancellationToken ct,
        Action<T>? seen = null
    )
        where T : TurboEntity, IHabboVersionEntity
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var known = await dbCtx.Set<T>().FirstOrDefaultAsync(same, ct).ConfigureAwait(false);

        if (known is not null)
        {
            known.CheckedAt = now;
            known.HabboHash = habboHash ?? known.HabboHash;
            seen?.Invoke(known);

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            return (known, false);
        }

        var version = create();

        dbCtx.Set<T>().Add(version);

        try
        {
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Another silo's check kept the same file a moment before this one.
            var retryCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var retryCtxScope = retryCtx.ConfigureAwait(false);

            var kept = await retryCtx
                .Set<T>()
                .AsNoTracking()
                .FirstOrDefaultAsync(same, ct)
                .ConfigureAwait(false);

            if (kept is not null)
                return (kept, false);

            logger.LogError(
                ex,
                "Failed to keep habbo.{Domain}'s {What} {Hash}",
                domain,
                what,
                hash
            );

            throw;
        }

        return (version, true);
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
