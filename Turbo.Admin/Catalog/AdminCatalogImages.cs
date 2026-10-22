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
/// The catalog's pictures - the headers and teasers its pages show - as the files in
/// <c>images/catalog</c> (<c>docs/asset-bundles.md</c>), however they got there, each with the
/// pages that show it. The Images tab and a page's picture picker both list them from here. An
/// upload is a GIF or a PNG, written into the folder as <c>&lt;name&gt;.gif</c>, the file the
/// client asks for, and recorded so a publish sends it; players see it once the assets are published.
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

    /// <summary>Pictures no page shows.</summary>
    public const string SHOW_UNUSED = "unused";

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

        return IsPictureFile(trimmed) ? trimmed : trimmed + GIF;
    }

    /// <summary>The name a page gives for a file: without its <c>.gif</c>.</summary>
    public static string NameOf(string file) =>
        file.EndsWith(GIF, StringComparison.OrdinalIgnoreCase) ? file[..^GIF.Length] : file;

    /// <summary>
    /// A page (from 0) of the pictures in the folder whose name holds <paramref name="query"/>, of
    /// those <paramref name="show"/> picks, by name; null when <paramref name="show"/> is neither.
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

        if (show is not (SHOW_ALL or SHOW_UNUSED))
            return null;

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var usage = await UsageAsync(db, ct).ConfigureAwait(false);
        var words = query?.Trim() ?? string.Empty;
        var matches = store
            .NamesOnDisk(KIND)
            .Where(file => IsPictureFile(file) && store.IsValidName(file))
            .Where(file =>
                words.Length == 0
                || NameOf(file).Contains(words, StringComparison.OrdinalIgnoreCase)
            )
            .Select(file => ItemOf(file, usage))
            .Where(x => show != SHOW_UNUSED || x.Pages.Length == 0)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        size = Math.Clamp(size, 1, PAGE_SIZE_MAX);

        return new CatalogImagesResponse(
            matches.Count,
            [.. matches.Skip(Math.Max(0, page) * size).Take(size)]
        );
    }

    /// <summary>
    /// The pictures pages show that the folder does not have, so no publish sends them and
    /// players see none: each with the pages that show it, by name.
    /// </summary>
    public async Task<IReadOnlyList<CatalogImageItem>> MissingAsync(CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var usage = await UsageAsync(db, ct).ConfigureAwait(false);
        var onDisk = store.NamesOnDisk(KIND);

        return
        [
            .. usage
                .Where(x => !onDisk.Contains(x.Key))
                .Select(x => new CatalogImageItem(NameOf(x.Key), x.Key, 0, null, x.Value))
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
        ];
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
    /// Writes an uploaded GIF or PNG into the folder as the picture <paramref name="name"/> (the
    /// file's name when none is given), replacing one of the name. Throws
    /// <see cref="ArgumentException"/>, with why, for a name, file or size it can't take.
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
        if (IsPictureFile(given))
            given = given[..^GIF.Length];

        var file = given + GIF;

        if (given.Length == 0 || !store.IsValidName(file))
            throw new ArgumentException(
                $"\"{given}\" can't be a picture's name: letters, digits, _ - and . only."
            );

        var hash = await store.WriteAsync(KIND, file, data, ct).ConfigureAwait(false);

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        // What a publish sends: the files recorded with their hash.
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

        return ItemOf(file, await UsageAsync(db, ct).ConfigureAwait(false));
    }

    /// <summary>Deletes a picture's file, and its record when it has one; false when it had neither.</summary>
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

    private static bool IsPictureFile(string file) =>
        file.EndsWith(GIF, StringComparison.OrdinalIgnoreCase)
        || file.EndsWith(PNG, StringComparison.OrdinalIgnoreCase);

    /// <summary>A file in the folder, as the file says, with the pages that show it.</summary>
    private CatalogImageItem ItemOf(
        string file,
        IReadOnlyDictionary<string, CatalogImagePage[]> usage
    )
    {
        var info = new FileInfo(store.FullPathOf(KIND, file));

        return new(
            NameOf(file),
            file,
            info.Exists ? info.Length : 0,
            info.Exists ? info.LastWriteTimeUtc : null,
            usage.TryGetValue(file, out var pages) ? pages : []
        );
    }

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
