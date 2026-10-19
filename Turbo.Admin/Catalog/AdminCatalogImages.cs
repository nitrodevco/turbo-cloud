using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Database.Entities.Assets;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Catalog;

/// <summary>
/// The catalog's pictures - the headers and teasers its pages show - as this server keeps them:
/// the files under <c>images/catalog</c> (<c>docs/asset-bundles.md</c>), whether a sync took them
/// from Habbo or staff uploaded them, with the pages that show each, and the names pages give that
/// have no file. An upload is a GIF or a PNG, kept as <c>&lt;name&gt;.gif</c>, the file the client
/// asks for; players see it once the assets are published.
/// </summary>
public sealed class AdminCatalogImages(
    IDbContextFactory<TurboDbContext> database,
    IAssetBundleStore store,
    TimeProvider time,
    ILogger<AdminCatalogImages> logger
)
{
    /// <summary>The most pictures a page of the list holds: enough for the picker to take them all.</summary>
    public const int PAGE_SIZE_MAX = 5000;

    /// <summary>The largest picture an upload may be.</summary>
    public const int UPLOAD_MAX_BYTES = 4 * 1024 * 1024;

    /// <summary>Every picture.</summary>
    public const string SHOW_ALL = "all";

    /// <summary>Pictures a page names that this server has no file for.</summary>
    public const string SHOW_MISSING = "missing";

    /// <summary>Pictures no page shows.</summary>
    public const string SHOW_UNUSED = "unused";

    /// <summary>Pictures staff uploaded.</summary>
    public const string SHOW_UPLOADED = "uploaded";

    private const string GIF = ".gif";
    private const string PNG = ".png";

    private const AssetBundleKind KIND = AssetBundleKind.CatalogImage;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// The file a page's picture is loaded from: the name with <c>.gif</c> after it, as the client's
    /// <c>asset.urls.catalog</c> asks, unless it already names a GIF or PNG.
    /// </summary>
    public static string FileOf(string name)
    {
        var trimmed = name.Trim();

        return
            trimmed.EndsWith(GIF, StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith(PNG, StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : trimmed + GIF;
    }

    /// <summary>The name a page gives for a file: without its <c>.gif</c>.</summary>
    public static string NameOf(string file) =>
        file.EndsWith(GIF, StringComparison.OrdinalIgnoreCase) ? file[..^GIF.Length] : file;

    /// <summary>
    /// A page (from 0) of the pictures whose name holds <paramref name="query"/>, of those
    /// <paramref name="show"/> picks, by name; null when <paramref name="show"/> is none of them.
    /// </summary>
    public async Task<CatalogImagesResponse?> ListAsync(
        string? query,
        string? show,
        int page,
        int size,
        CancellationToken ct
    )
    {
        show = string.IsNullOrWhiteSpace(show) ? SHOW_ALL : show.Trim().ToLowerInvariant();

        if (show is not (SHOW_ALL or SHOW_MISSING or SHOW_UNUSED or SHOW_UPLOADED))
            return null;

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var rows = await db
            .AssetBundles.AsNoTracking()
            .Where(x => x.Kind == KIND)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var usage = await UsageAsync(db, ct).ConfigureAwait(false);
        var items = new Dictionary<string, CatalogImageItem>(StringComparer.Ordinal);

        foreach (var row in rows)
            items[row.Name] = ItemOf(row, usage);

        // Named by a page, never fetched: missing, with nothing known of it.
        foreach (var (file, pages) in usage)
        {
            if (!items.ContainsKey(file))
                items[file] = new CatalogImageItem(NameOf(file), file, null, false, 0, null, pages);
        }

        var words = query?.Trim() ?? string.Empty;
        var matches = items
            .Values.Where(x =>
                words.Length == 0 || x.Name.Contains(words, StringComparison.OrdinalIgnoreCase)
            )
            .Where(x =>
                show switch
                {
                    SHOW_MISSING => !x.HasFile,
                    SHOW_UNUSED => x.Pages.Length == 0,
                    SHOW_UPLOADED => x.Source == AssetContractNames.Of(AssetBundleSource.Upload),
                    _ => true,
                }
            )
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        size = Math.Clamp(size, 1, PAGE_SIZE_MAX);

        return new CatalogImagesResponse(
            matches.Count,
            [.. matches.Skip(Math.Max(0, page) * size).Take(size)]
        );
    }

    /// <summary>A picture's file on this server; null when it has none.</summary>
    public string? FilePathOf(string name)
    {
        var file = FileOf(name);

        if (!store.IsValidName(file))
            return null;

        var path = store.FullPathOf(KIND, file);

        return File.Exists(path) ? path : null;
    }

    /// <summary>A picture's content type, by its first bytes: PNG or GIF.</summary>
    public static string ContentTypeOf(ReadOnlySpan<byte> head) =>
        head.StartsWith(PngSignature) ? "image/png" : "image/gif";

    /// <summary>
    /// Keeps an uploaded GIF or PNG as the picture <paramref name="name"/> (the file's name when
    /// none is given), replacing one of the name. Throws <see cref="ArgumentException"/>, with why,
    /// for a name, file or size it can't take.
    /// </summary>
    public async Task<CatalogImageItem> UploadAsync(
        string? name,
        string fileName,
        byte[] data,
        PlayerId player,
        CancellationToken ct
    )
    {
        if (data.Length == 0)
            throw new ArgumentException("The file is empty.");

        if (data.Length > UPLOAD_MAX_BYTES)
            throw new ArgumentException(
                $"A picture is at most {UPLOAD_MAX_BYTES / (1024 * 1024)} MB."
            );

        if (!IsGif(data) && !data.AsSpan().StartsWith(PngSignature))
            throw new ArgumentException($"{fileName} is not a GIF or a PNG.");

        var given = string.IsNullOrWhiteSpace(name)
            ? Path.GetFileNameWithoutExtension(fileName)
            : name.Trim();

        // The client asks for <name>.gif, so that is the file, whichever the picture is.
        if (
            given.EndsWith(GIF, StringComparison.OrdinalIgnoreCase)
            || given.EndsWith(PNG, StringComparison.OrdinalIgnoreCase)
        )
            given = given[..^GIF.Length];

        var file = given + GIF;

        if (given.Length == 0 || !store.IsValidName(file))
            throw new ArgumentException(
                $"\"{given}\" can't be a picture's name: letters, digits, _ - and . only."
            );

        var hash = await store.WriteAsync(KIND, file, data, ct).ConfigureAwait(false);

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var row = await db
            .AssetBundles.FirstOrDefaultAsync(x => x.Kind == KIND && x.Name == file, ct)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new AssetBundleEntity
            {
                Kind = KIND,
                Name = file,
                Source = AssetBundleSource.Upload,
            };
            db.AssetBundles.Add(row);
        }

        row.Source = AssetBundleSource.Upload;
        row.Revision = null;
        row.Hash = hash;
        row.Size = data.LongLength;
        row.Error = null;
        row.Retry = false;
        row.UpdatedAt = time.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Player {PlayerId} uploaded the catalog picture {File} ({Hash})",
            player,
            file,
            hash
        );

        return ItemOf(row, await UsageAsync(db, ct).ConfigureAwait(false));
    }

    /// <summary>Deletes a picture's file and row; false when it had neither.</summary>
    public async Task<bool> DeleteAsync(string name, PlayerId player, CancellationToken ct)
    {
        var file = FileOf(name);

        if (!store.IsValidName(file))
            return false;

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var row = await db
            .AssetBundles.FirstOrDefaultAsync(x => x.Kind == KIND && x.Name == file, ct)
            .ConfigureAwait(false);
        var hadFile = File.Exists(store.FullPathOf(KIND, file));

        if (row is null && !hadFile)
            return false;

        store.Delete(KIND, file);

        if (row is not null)
        {
            db.AssetBundles.Remove(row);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        logger.LogInformation("Player {PlayerId} deleted the catalog picture {File}", player, file);

        return true;
    }

    private static bool IsGif(ReadOnlySpan<byte> data) =>
        data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8);

    private CatalogImageItem ItemOf(
        AssetBundleEntity row,
        IReadOnlyDictionary<string, CatalogImagePage[]> usage
    ) =>
        new(
            NameOf(row.Name),
            row.Name,
            AssetContractNames.Of(row.Source),
            row.Hash is not null
                && row.Error is null
                && File.Exists(store.FullPathOf(KIND, row.Name)),
            row.Size,
            DateTime.SpecifyKind(row.UpdatedAt, DateTimeKind.Utc),
            usage.TryGetValue(row.Name, out var pages) ? pages : []
        );

    /// <summary>The pages that show each picture, by its file.</summary>
    private async Task<Dictionary<string, CatalogImagePage[]>> UsageAsync(
        TurboDbContext db,
        CancellationToken ct
    )
    {
        var pages = await db
            .CatalogPages.AsNoTracking()
            .Where(x => x.ImageData != null)
            .Select(x => new
            {
                x.Id,
                x.Localization,
                x.ImageData,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return pages
            .SelectMany(page =>
                (page.ImageData ?? [])
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0 && store.IsValidName(FileOf(x)))
                    .Select(FileOf)
                    .Distinct(StringComparer.Ordinal)
                    .Select(file =>
                        (File: file, Page: new CatalogImagePage(page.Id, page.Localization))
                    )
            )
            .GroupBy(x => x.File, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x =>
                    x.Select(y => y.Page)
                        .OrderBy(y => y.Title, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                StringComparer.Ordinal
            );
    }
}
