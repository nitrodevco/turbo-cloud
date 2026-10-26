using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Assets;
using Turbo.Admin.Catalog;
using Turbo.Database.Context;
using Turbo.Database.Entities.Assets;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;

namespace Turbo.Admin.Media;

/// <summary>
/// One index of every image the hotel can show (<c>docs/media-library.md</c>), whatever keeps it:
/// the client's image library (<c>c_images</c>) and the catalog's, badges' and furniture's pictures
/// in the asset folder (each from Habbo, an upload or put there by hand), the client's own images
/// (the window manager's, which notices name), and each plugin's <c>wwwroot/styles/</c> images.
/// Each image says what each kind of field stores to show it, and where the hotel shows it:
/// notices and other variables, hotel view articles, catalog pages, icons and front page,
/// campaigns and theme values. An image anything shows can't be deleted or renamed. Uploads go
/// into the asset folder with a record, so a publish sends them where the client loads them.
/// </summary>
public sealed class MediaLibrary(
    IDbContextFactory<TurboDbContext> database,
    IAssetBundleStore store,
    AdminCatalogImages catalogImages,
    TimeProvider time,
    ILogger<MediaLibrary> logger,
    ClientAssets? addresses = null,
    IHotelDataPlugin? hotel = null,
    IPluginAdministration? plugins = null
)
{
    /// <summary>The most images a page of a search holds.</summary>
    public const int PAGE_SIZE_MAX = 500;

    /// <summary>The largest image an upload may be.</summary>
    public const int UPLOAD_MAX_BYTES = 4 * 1024 * 1024;

    /// <summary>The folder under the image library an upload goes to when none is chosen.</summary>
    public const string UPLOAD_FOLDER = "uploads";

    /// <summary>The kind an upload names to be a catalog picture (<c>images/catalog</c>) rather than a library image.</summary>
    public const string KIND_CATALOG_PICTURE = "catalogue-picture";

    public const string SOURCE_LIBRARY = "library";
    public const string SOURCE_ASSETS = "assets";
    public const string SOURCE_CLIENT = "client";
    public const string SOURCE_PLUGIN = "plugin";
    public const string SOURCE_UPLOAD = "upload";
    public const string SOURCE_HABBO = "habbo";

    // The client library whose PNGs a notice names (`static_bitmap.assetUri` without a `/`).
    private const string WINDOW_LIBRARY = "habbo-window-manager-com";

    private const string NOTICE_PREFIX = "notifications.new_feature.";
    private const string NOTICE_IMAGE = NOTICE_PREFIX + "image.";

    // How long a built index answers before the folders and records are read again; a change made
    // here drops it at once.
    private static readonly TimeSpan IndexLifetime = TimeSpan.FromSeconds(15);

    private static readonly string[] Extensions = [".png", ".gif", ".jpg", ".jpeg", ".webp"];

    private static readonly (AssetBundleKind Kind, string Prefix)[] StoreKinds =
    [
        (AssetBundleKind.LibraryImage, "library"),
        (AssetBundleKind.CatalogImage, "catalog"),
        (AssetBundleKind.CatalogIcon, "catalog-icon"),
        (AssetBundleKind.Badge, "badge"),
        (AssetBundleKind.FurniIcon, "furni-icon"),
    ];

    private readonly ConcurrentDictionary<string, (long, DateTime, (int, int)?)> _sizes = new();
    private readonly SemaphoreSlim _tagsLock = new(1, 1);
    private Index? _index;

    /// <summary>A page (from 0) of the images the search finds, with how many of each kind and source it found.</summary>
    public async Task<MediaSearchResponse> SearchAsync(MediaQuery query, CancellationToken ct)
    {
        var index = await IndexAsync(ct).ConfigureAwait(false);
        var words = (query.Text ?? "").Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        var found = index
            .Entries.Where(x =>
                words.All(word =>
                    x.Path.Contains(word, StringComparison.OrdinalIgnoreCase)
                    || x.Tags.Any(tag => tag.Contains(word, StringComparison.OrdinalIgnoreCase))
                    || (x.SourceDetail?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false)
                )
            )
            .ToList();
        var facets = new MediaFacets(Count(found, x => x.Kind), Count(found, x => x.Source));
        var kinds = ListOf(query.Kind);
        var sources = ListOf(query.Source);
        var shapes = ListOf(query.Shape);
        var used = query.Used?.Trim().ToLowerInvariant();

        found =
        [
            .. found.Where(x =>
                (kinds.Count == 0 || kinds.Contains(x.Kind))
                && (sources.Count == 0 || sources.Contains(x.Source))
                && (shapes.Count == 0 || shapes.Any(shape => HasShape(x.Values, shape)))
                && (
                    used switch
                    {
                        "used" => index.UsesOf(x.Id).Count > 0,
                        "unused" => index.UsesOf(x.Id).Count == 0,
                        _ => true,
                    }
                )
                && (query.MinWidth is not { } minW || x.Width >= minW)
                && (query.MaxWidth is not { } maxW || x.Width <= maxW)
                && (query.MinHeight is not { } minH || x.Height >= minH)
                && (query.MaxHeight is not { } maxH || x.Height <= maxH)
            ),
        ];

        IEnumerable<Entry> sorted = query.Sort?.Trim().ToLowerInvariant() switch
        {
            "modified" => found.OrderBy(x => x.Modified ?? DateTime.MinValue),
            "size" => found.OrderBy(x => x.Bytes),
            "used" => found.OrderBy(x => index.UsesOf(x.Id).Count),
            _ => found.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase),
        };

        if (query.Descending)
            sorted = sorted.Reverse();

        var size = Math.Clamp(query.Size, 1, PAGE_SIZE_MAX);

        return new MediaSearchResponse(
            found.Count,
            [
                .. sorted
                    .Skip(Math.Max(0, query.Page) * size)
                    .Take(size)
                    .Select(x => ImageOf(x, index, withUses: false)),
            ],
            facets
        );
    }

    /// <summary>One image with every place that shows it; null for no image with the id.</summary>
    public async Task<MediaImage?> GetAsync(string id, CancellationToken ct)
    {
        var index = await IndexAsync(ct).ConfigureAwait(false);

        return index.Find(id) is { } entry ? ImageOf(entry, index, withUses: true) : null;
    }

    /// <summary>An image's bytes and content type; null for no image with the id, or one whose file went.</summary>
    public async Task<(byte[] Data, string ContentType)?> ReadAsync(string id, CancellationToken ct)
    {
        var index = await IndexAsync(ct).ConfigureAwait(false);

        if (index.Find(id) is not { } entry)
            return null;

        var data = await entry.Read(ct).ConfigureAwait(false);

        return data is null
            ? null
            : (data, MediaImageSize.ContentTypeOf(MediaImageSize.TypeOf(data)));
    }

    /// <summary>
    /// Keeps each file in the image library's <paramref name="folder"/> (<see cref="UPLOAD_FOLDER"/>
    /// when none), or as a catalog picture for <see cref="KIND_CATALOG_PICTURE"/>, recorded so a
    /// publish sends it; a file that is no PNG, GIF, JPEG or WebP of its name's type, too large or
    /// badly named is refused with why, and the rest kept.
    /// </summary>
    public async Task<MediaUploadResponse> UploadAsync(
        IReadOnlyList<(string FileName, byte[] Data)> files,
        string? folder,
        string? kind,
        PlayerId player,
        CancellationToken ct
    )
    {
        var kept = new List<string>();
        var refused = new List<MediaRefusal>();
        var under = (folder ?? "").Trim().Trim('/').Replace('\\', '/');

        if (under.Length == 0)
            under = UPLOAD_FOLDER;

        foreach (var (fileName, data) in files)
        {
            var name = Path.GetFileName(fileName.Replace('\\', '/'));

            if (Problem(name, data) is { } why)
            {
                refused.Add(new(fileName, why));
                continue;
            }

            try
            {
                if (
                    string.Equals(
                        kind?.Trim(),
                        KIND_CATALOG_PICTURE,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    var picture = await catalogImages
                        .UploadAsync(null, name, data, player, ct)
                        .ConfigureAwait(false);

                    kept.Add($"catalog:{picture.File}");
                    continue;
                }

                var path = $"{under}/{name}";

                if (!store.IsValidName(AssetBundleKind.LibraryImage, path))
                {
                    refused.Add(
                        new(
                            fileName,
                            $"\"{path}\" can't be a path: letters, digits, _ - . and / only."
                        )
                    );
                    continue;
                }

                await KeepAsync(AssetBundleKind.LibraryImage, path, data, ct).ConfigureAwait(false);
                kept.Add($"library:{path}");

                logger.LogInformation(
                    "Player {PlayerId} uploaded the image {Path} to the media library",
                    player,
                    path
                );
            }
            catch (ArgumentException ex)
            {
                refused.Add(new(fileName, ex.Message));
            }
        }

        _index = null;

        var index = await IndexAsync(ct).ConfigureAwait(false);

        return new MediaUploadResponse(
            [
                .. kept.Select(index.Find)
                    .OfType<Entry>()
                    .Select(x => ImageOf(x, index, withUses: false)),
            ],
            refused
        );
    }

    /// <summary>Renames an image (its folder kept) and sets its tags; see <see cref="MediaEditOutcome"/>.</summary>
    public async Task<MediaEditOutcome> EditAsync(
        string id,
        MediaEditRequest request,
        PlayerId player,
        CancellationToken ct
    )
    {
        var index = await IndexAsync(ct).ConfigureAwait(false);

        if (index.Find(id) is not { } entry)
            return MediaEditOutcome.Missing;

        var newName = request.Name?.Trim();

        if (!string.IsNullOrEmpty(newName) && newName != entry.Name)
        {
            if (entry.StoreKind is not AssetBundleKind.LibraryImage)
                return MediaEditOutcome.Refused(
                    "Only an image library picture can be renamed here: the others are named by what shows them."
                );

            if (index.UsesOf(id) is { Count: > 0 } uses)
                return MediaEditOutcome.InUse(uses);

            var slash = entry.Path.LastIndexOf('/');
            var path = slash < 0 ? newName : $"{entry.Path[..(slash + 1)]}{newName}";

            if (
                Path.GetExtension(path).ToLowerInvariant()
                != Path.GetExtension(entry.Path).ToLowerInvariant()
            )
                return MediaEditOutcome.Refused("A new name keeps the file's type.");

            if (!store.IsValidName(AssetBundleKind.LibraryImage, path))
                return MediaEditOutcome.Refused(
                    $"\"{newName}\" can't be a name: letters, digits, _ - and . only."
                );

            if (store.NamesOnDisk(AssetBundleKind.LibraryImage).Contains(path))
                return MediaEditOutcome.Refused($"There is an image at {path} already.");

            var data = await entry.Read(ct).ConfigureAwait(false);

            if (data is null)
                return MediaEditOutcome.Missing;

            await KeepAsync(AssetBundleKind.LibraryImage, path, data, ct).ConfigureAwait(false);
            await ForgetAsync(AssetBundleKind.LibraryImage, entry.Path, ct).ConfigureAwait(false);
            await MoveTagsAsync(id, $"library:{path}", ct).ConfigureAwait(false);

            logger.LogInformation(
                "Player {PlayerId} renamed the image {From} to {To}",
                player,
                entry.Path,
                path
            );

            id = $"library:{path}";
        }

        if (request.Tags is { } tags)
            await SetTagsAsync(id, tags, ct).ConfigureAwait(false);

        _index = null;
        index = await IndexAsync(ct).ConfigureAwait(false);

        return index.Find(id) is { } edited
            ? MediaEditOutcome.Done(ImageOf(edited, index, withUses: true))
            : MediaEditOutcome.Missing;
    }

    /// <summary>Deletes an image the asset folder keeps, unless anything shows it; see <see cref="MediaEditOutcome"/>.</summary>
    public async Task<MediaEditOutcome> DeleteAsync(
        string id,
        PlayerId player,
        CancellationToken ct
    )
    {
        var index = await IndexAsync(ct).ConfigureAwait(false);

        if (index.Find(id) is not { } entry)
            return MediaEditOutcome.Missing;

        if (entry.StoreKind is not { } kind)
            return MediaEditOutcome.Refused(
                entry.Source == SOURCE_PLUGIN
                    ? "A plugin's image is taken away on the plugin's Theme tab."
                    : "The client's own images come with the client."
            );

        if (index.UsesOf(id) is { Count: > 0 } uses)
            return MediaEditOutcome.InUse(uses);

        await ForgetAsync(kind, entry.Path, ct).ConfigureAwait(false);
        await SetTagsAsync(id, [], ct).ConfigureAwait(false);

        logger.LogInformation("Player {PlayerId} deleted the image {Id}", player, id);

        _index = null;

        return MediaEditOutcome.Done(null);
    }

    private static string? Problem(string name, byte[] data)
    {
        if (data.Length == 0)
            return "The file is empty.";

        if (data.Length > UPLOAD_MAX_BYTES)
            return $"An image is at most {UPLOAD_MAX_BYTES / (1024 * 1024)} MB.";

        var type = MediaImageSize.TypeOf(data);
        var extension = Path.GetExtension(name).ToLowerInvariant();

        if (type is null || !Extensions.Contains(extension))
            return $"{name} is not a PNG, GIF, JPEG or WebP.";

        if ((type == "jpeg" ? extension is not (".jpg" or ".jpeg") : extension != $".{type}"))
            return $"{name} is a {type.ToUpperInvariant()}: name it .{(type == "jpeg" ? "jpg" : type)}.";

        return null;
    }

    private async Task KeepAsync(
        AssetBundleKind kind,
        string name,
        byte[] data,
        CancellationToken ct
    )
    {
        var hash = await store.WriteAsync(kind, name, data, ct).ConfigureAwait(false);
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        // What a publish sends: the files recorded with their hash.
        var row = await db
            .AssetBundles.FirstOrDefaultAsync(x => x.Kind == kind && x.Name == name, ct)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new AssetBundleEntity
            {
                Kind = kind,
                Name = name,
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
    }

    private async Task ForgetAsync(AssetBundleKind kind, string name, CancellationToken ct)
    {
        store.Delete(kind, name);

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        await db
            .AssetBundles.Where(x => x.Kind == kind && x.Name == name)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
    }

    private static bool HasShape(MediaValues values, string shape) =>
        shape switch
        {
            "relative" => values.Relative is not null,
            "catalogpicture" => values.CatalogPicture is not null,
            "catalogicon" => values.CatalogIcon is not null,
            "clientimage" => values.ClientImage is not null,
            "badge" => values.Badge is not null,
            "plugin" => values.Plugin is not null,
            "url" => values.Url.Length > 0,
            _ => false,
        };

    private static HashSet<string> ListOf(string? list) =>
        [
            .. (list ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.ToLowerInvariant()),
        ];

    private static Dictionary<string, int> Count(
        IEnumerable<Entry> entries,
        Func<Entry, string> by
    ) =>
        entries
            .GroupBy(by)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count());

    private static MediaImage ImageOf(Entry entry, Index index, bool withUses)
    {
        var uses = index.UsesOf(entry.Id);

        return new MediaImage(
            entry.Id,
            entry.Name,
            entry.Path,
            entry.Kind,
            entry.Source,
            entry.SourceDetail,
            entry.Values.Url,
            $"/media/file?id={Uri.EscapeDataString(entry.Id)}",
            entry.Width,
            entry.Height,
            entry.Bytes,
            entry.Modified,
            entry.Tags,
            uses.Count,
            entry.Values,
            withUses ? uses : null
        );
    }

    private async Task<Index> IndexAsync(CancellationToken ct)
    {
        if (_index is { } kept && time.GetUtcNow() - kept.Built < IndexLifetime)
            return kept;

        var built = await BuildAsync(ct).ConfigureAwait(false);

        _index = built;

        return built;
    }

    private async Task<Index> BuildAsync(CancellationToken ct)
    {
        var assets = addresses is null
            ? ClientAssetsResponse.NONE
            : await addresses.GetAsync(ct).ConfigureAwait(false);
        var tags = await ReadTagsAsync(ct).ConfigureAwait(false);
        var entries = new List<Entry>();

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var rows = (
            await db
                .AssetBundles.AsNoTracking()
                .Where(x => StoreKinds.Select(y => y.Kind).Contains(x.Kind))
                .Select(x => new
                {
                    x.Kind,
                    x.Name,
                    x.Source,
                    x.Revision,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        ).GroupBy(x => (x.Kind, x.Name)).ToDictionary(x => x.Key, x => x.First());

        foreach (var (kind, prefix) in StoreKinds)
        {
            foreach (
                var name in store
                    .NamesOnDisk(kind)
                    .Where(IsImage)
                    .Where(x => store.IsValidName(kind, x))
            )
            {
                var full = store.FullPathOf(kind, name);
                var info = new FileInfo(full);
                var row = rows.GetValueOrDefault((kind, name));
                var id = $"{prefix}:{name}";

                entries.Add(
                    new Entry(
                        id,
                        name[(name.LastIndexOf('/') + 1)..],
                        name,
                        KindOf(kind, name),
                        row?.Source switch
                        {
                            AssetBundleSource.Habbo => SOURCE_HABBO,
                            AssetBundleSource.Upload => SOURCE_UPLOAD,
                            _ => kind == AssetBundleKind.LibraryImage
                                ? SOURCE_LIBRARY
                                : SOURCE_ASSETS,
                        },
                        row?.Source == AssetBundleSource.Habbo ? row.Revision : null,
                        ValuesOf(kind, name, assets),
                        kind,
                        store.PathOf(kind, name),
                        info.Exists ? info.Length : 0,
                        info.Exists ? info.LastWriteTimeUtc : null,
                        SizeOf(full, info),
                        tags.GetValueOrDefault(id) ?? [],
                        c => ReadFileAsync(full, c)
                    )
                );
            }
        }

        entries.AddRange(ClientImages(tags));
        entries.AddRange(await PluginImagesAsync(tags, ct).ConfigureAwait(false));

        var uses = await UsesAsync(db, entries, ct).ConfigureAwait(false);

        return new Index(time.GetUtcNow(), entries, uses);
    }

    private (int, int)? SizeOf(string full, FileInfo info)
    {
        if (!info.Exists)
            return null;

        if (
            _sizes.TryGetValue(full, out var known)
            && known.Item1 == info.Length
            && known.Item2 == info.LastWriteTimeUtc
        )
            return known.Item3;

        var size = MediaImageSize.SizeOfFile(full);

        _sizes[full] = (info.Length, info.LastWriteTimeUtc, size);

        return size;
    }

    private static async Task<byte[]?> ReadFileAsync(string path, CancellationToken ct)
    {
        try
        {
            return await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static bool IsImage(string name) =>
        Extensions.Contains(Path.GetExtension(name).ToLowerInvariant());

    private static string KindOf(AssetBundleKind kind, string name) =>
        kind switch
        {
            AssetBundleKind.CatalogImage => KIND_CATALOG_PICTURE,
            AssetBundleKind.CatalogIcon => "catalogue-icon",
            AssetBundleKind.Badge => "badge",
            AssetBundleKind.FurniIcon => "furni-icon",
            _ => name.Split('/')[0].ToLowerInvariant() switch
            {
                var top when top.StartsWith("web_promo", StringComparison.Ordinal) => "promo",
                "reception" or "hotelview" or "hotel_view" => "reception",
                var top when top.StartsWith("quest", StringComparison.Ordinal) => "quest",
                "catalogue" => KIND_CATALOG_PICTURE,
                var top when top.StartsWith("album", StringComparison.Ordinal) => "badge",
                _ => "other",
            },
        };

    private static MediaValues ValuesOf(
        AssetBundleKind kind,
        string name,
        ClientAssetsResponse assets
    )
    {
        var bare = Path.GetFileNameWithoutExtension(name);

        return kind switch
        {
            AssetBundleKind.LibraryImage => new(
                name,
                null,
                null,
                null,
                null,
                null,
                assets.ImageLibrary.Length > 0 ? assets.ImageLibrary + name : ""
            ),
            AssetBundleKind.CatalogImage => new(
                null,
                AdminCatalogImages.NameOf(name),
                null,
                null,
                null,
                null,
                Fill(assets.CatalogImage, "%name%", AdminCatalogImages.NameOf(name))
            ),
            AssetBundleKind.CatalogIcon => new(
                null,
                null,
                bare.StartsWith("icon_", StringComparison.Ordinal)
                && int.TryParse(bare["icon_".Length..], out var icon)
                    ? icon
                    : null,
                null,
                null,
                null,
                bare.StartsWith("icon_", StringComparison.Ordinal)
                    ? Fill(assets.CatalogIcon, "%name%", bare["icon_".Length..])
                    : ""
            ),
            AssetBundleKind.Badge => new(
                null,
                null,
                null,
                null,
                bare,
                null,
                Fill(assets.Badge, "%badgename%", bare)
            ),
            _ => new(null, null, null, null, null, null, ""),
        };
    }

    private static string Fill(string template, string placeholder, string value) =>
        template.Contains(placeholder, StringComparison.Ordinal)
            ? Absolute(template.Replace(placeholder, value, StringComparison.Ordinal))
            : "";

    private static string Absolute(string url) =>
        url.StartsWith("//", StringComparison.Ordinal) ? $"https:{url}" : url;

    /// <summary>The window manager's PNGs, by the name a notice gives: what the template bundle in the asset folder holds.</summary>
    private IEnumerable<Entry> ClientImages(IReadOnlyDictionary<string, string[]> tags)
    {
        if (!store.IsValidName(WINDOW_LIBRARY))
            yield break;

        var full = store.FullPathOf(AssetBundleKind.Template, WINDOW_LIBRARY);

        if (!File.Exists(full))
            yield break;

        Dictionary<string, byte[]> images;

        try
        {
            using var zip = ZipFile.OpenRead(full);

            images = zip
                .Entries.Where(x =>
                    x.Name.StartsWith(WINDOW_LIBRARY + "-", StringComparison.Ordinal)
                    && x.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                )
                .ToDictionary(
                    x => x.Name[(WINDOW_LIBRARY.Length + 1)..^".png".Length],
                    x =>
                    {
                        using var stream = x.Open();
                        using var memory = new MemoryStream();

                        stream.CopyTo(memory);

                        return memory.ToArray();
                    },
                    StringComparer.Ordinal
                );
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            logger.LogWarning(
                ex,
                "The client's {Library} bundle could not be read",
                WINDOW_LIBRARY
            );
            yield break;
        }

        var modified = File.GetLastWriteTimeUtc(full);

        foreach (var (name, data) in images.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var id = $"client:{name}";

            yield return new Entry(
                id,
                name,
                $"{WINDOW_LIBRARY}/{name}.png",
                "client-image",
                SOURCE_CLIENT,
                WINDOW_LIBRARY,
                new(null, null, null, name, null, null, ""),
                null,
                null,
                data.LongLength,
                modified,
                MediaImageSize.SizeOf(data),
                tags.GetValueOrDefault(id) ?? [],
                _ => Task.FromResult<byte[]?>(data)
            );
        }
    }

    /// <summary>Each plugin's <c>wwwroot/styles/</c> images, the hotel's own data plugin's among them.</summary>
    private async Task<List<Entry>> PluginImagesAsync(
        IReadOnlyDictionary<string, string[]> tags,
        CancellationToken ct
    )
    {
        var entries = new List<Entry>();

        if (hotel is null)
            return entries;

        var keys = (plugins?.List().Select(x => x.Key) ?? [])
            .Append(IHotelDataPlugin.KEY)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys)
        {
            if (hotel.FolderOf(key) is not { } folder)
                continue;

            var images = await hotel.ImagesAsync(key, ct).ConfigureAwait(false);

            foreach (var image in images ?? [])
            {
                var full = Path.Combine(folder, "wwwroot", image.Path);
                var info = new FileInfo(full);
                var id = $"plugin:{key}/{image.Path}";

                entries.Add(
                    new Entry(
                        id,
                        image.Path[(image.Path.LastIndexOf('/') + 1)..],
                        $"{key}/{image.Path}",
                        "theme",
                        SOURCE_PLUGIN,
                        key,
                        new(null, null, null, null, null, image.Path, Absolute(image.Url ?? "")),
                        null,
                        null,
                        image.Bytes,
                        image.Modified.UtcDateTime,
                        SizeOf(full, info),
                        tags.GetValueOrDefault(id) ?? [],
                        c => ReadFileAsync(full, c)
                    )
                );
            }
        }

        return entries;
    }

    /// <summary>
    /// Where the hotel shows each image, by its id: every value naming an image as one of its
    /// fields stores it (a path under the image library, a catalog picture's name, a client image's
    /// name, a full address) is matched by the image's path where it is kept.
    /// </summary>
    private static async Task<Dictionary<string, List<MediaUse>>> UsesAsync(
        TurboDbContext db,
        IReadOnlyList<Entry> entries,
        CancellationToken ct
    )
    {
        // Each image by every key a value naming it can come down to: where it is kept (and, for
        // the image library, its path under it), lowercased as hosts are lenient with case.
        var byKey = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        void Key(string key, string id)
        {
            if (!byKey.TryGetValue(key, out var ids))
                byKey[key] = ids = [];

            ids.Add(id);
        }

        foreach (var entry in entries)
        {
            if (entry.StorePath is { } kept)
                Key(kept, entry.Id);

            if (entry.Values.Relative is { } relative)
                Key(relative, entry.Id);

            if (entry.Values.ClientImage is { } client)
                Key($"client:{client}", entry.Id);

            if (entry.Source == SOURCE_PLUGIN)
                Key($"plugin:{entry.Path}", entry.Id);
        }

        var uses = new Dictionary<string, List<MediaUse>>(StringComparer.Ordinal);

        void Use(IEnumerable<string> keys, MediaUse use)
        {
            foreach (
                var id in keys.SelectMany(key => byKey.GetValueOrDefault(key) ?? []).Distinct()
            )
            {
                if (!uses.TryGetValue(id, out var list))
                    uses[id] = list = [];

                if (!list.Contains(use))
                    list.Add(use);
            }
        }

        var variables = await db
            .GamedataVariables.AsNoTracking()
            .Select(x => new { x.Key, x.Value })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var variable in variables)
        {
            foreach (var value in StringsOf(variable.Value))
            {
                if (variable.Key.StartsWith(NOTICE_IMAGE, StringComparison.Ordinal))
                {
                    var key = variable.Key[NOTICE_IMAGE.Length..];
                    var keys = KeysOf(value).ToList();

                    if (!value.Contains('/', StringComparison.Ordinal))
                        keys.Add($"client:{value.Trim()}");

                    Use(
                        keys,
                        new(
                            "notice",
                            key,
                            key,
                            "image",
                            $"/notices?key={Uri.EscapeDataString(key)}"
                        )
                    );
                }
                else
                {
                    Use(
                        KeysOf(value),
                        new(
                            AreaOf(variable.Key),
                            variable.Key,
                            variable.Key,
                            variable.Key,
                            $"/gamedata/variables?q={Uri.EscapeDataString(variable.Key)}"
                        )
                    );
                }
            }
        }

        foreach (
            var article in await db
                .PromoArticles.AsNoTracking()
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.ImageUrl,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            Use(
                KeysOf(article.ImageUrl),
                new(
                    "hotel-view",
                    article.Id.ToString(),
                    article.Title,
                    "article image",
                    "/hotel-view"
                )
            );

        foreach (
            var page in await db
                .CatalogPages.AsNoTracking()
                .Select(x => new
                {
                    x.Id,
                    x.Localization,
                    x.Icon,
                    x.ImageData,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
        {
            var link = $"/catalog?page={page.Id}";

            foreach (var picture in page.ImageData ?? [])
                if (picture.Trim().Length > 0)
                    Use(
                        [
                            $"images/catalog/{AdminCatalogImages.FileOf(picture)}",
                            .. KeysOf(picture),
                        ],
                        new("catalog-page", page.Id.ToString(), page.Localization, "picture", link)
                    );

            if (page.Icon > 0)
                Use(
                    [$"images/catalog-icons/icon_{page.Icon}.png"],
                    new("catalog-icon", page.Id.ToString(), page.Localization, "icon", link)
                );
        }

        foreach (
            var item in await db
                .CatalogFeaturedItems.AsNoTracking()
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.Image,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            Use(
                KeysOf(item.Image),
                new("catalog-featured", item.Id.ToString(), item.Title, "image", "/catalog")
            );

        foreach (
            var calendar in await db
                .CampaignCalendars.AsNoTracking()
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Image,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            Use(
                KeysOf(calendar.Image),
                new("campaign", calendar.Id.ToString(), calendar.Name, "image", "/campaigns")
            );

        foreach (
            var reward in await db
                .CampaignCalendarRewards.AsNoTracking()
                .Select(x => new
                {
                    x.CampaignCalendarEntityId,
                    x.DayIndex,
                    x.CustomImage,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            Use(
                KeysOf(reward.CustomImage),
                new(
                    "campaign",
                    reward.CampaignCalendarEntityId.ToString(),
                    $"Day {reward.DayIndex + 1}",
                    "gift image",
                    "/campaigns"
                )
            );

        foreach (
            var value in await db
                .GamedataPluginValues.AsNoTracking()
                .Select(x => new
                {
                    x.PluginKey,
                    x.Key,
                    x.Value,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
        foreach (var text in StringsOf(value.Value))
            Use(
                [.. KeysOf(text), $"plugin:{value.PluginKey}/{text.Trim().TrimStart('/')}"],
                new(
                    "theme",
                    value.PluginKey,
                    value.PluginKey,
                    value.Key,
                    $"/appearance/themes?plugin={Uri.EscapeDataString(value.PluginKey)}"
                )
            );

        return uses;
    }

    private static string AreaOf(string key) =>
        key.StartsWith("landing.view", StringComparison.OrdinalIgnoreCase)
        || key.StartsWith("hotelview", StringComparison.OrdinalIgnoreCase)
        || key.StartsWith("hotel.view", StringComparison.OrdinalIgnoreCase)
            ? "hotel-view"
        : key.StartsWith(NOTICE_PREFIX, StringComparison.Ordinal) ? "notice"
        : "variable";

    /// <summary>The strings a stored value holds: the value itself, or each string in it when it is JSON.</summary>
    private static IEnumerable<string> StringsOf(string value)
    {
        var trimmed = value.Trim();

        if (trimmed.Length == 0)
            return [];

        if (trimmed[0] is not ('"' or '[' or '{'))
            return [trimmed];

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var found = new List<string>();

            Collect(doc.RootElement, found);

            return found;
        }
        catch (JsonException)
        {
            return [trimmed];
        }
    }

    private static void Collect(JsonElement element, List<string> found)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                found.Add(element.GetString()!);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    Collect(item, found);
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    Collect(property.Value, found);
                break;
        }
    }

    /// <summary>
    /// The keys a value naming an image comes down to: the path itself, after a leading
    /// <c>${variable}</c> (the image library's address), and for a full address its path on the
    /// host and every shorter path at its end, so <c>https://host/c_images/web_promo/x.png</c>
    /// finds the library's <c>c_images/web_promo/x.png</c>.
    /// </summary>
    internal static IEnumerable<string> KeysOf(string value)
    {
        var text = value.Trim();

        if (text.Length == 0 || text.Length > 2048)
            yield break;

        if (
            text.StartsWith("${", StringComparison.Ordinal)
            && text.IndexOf('}') is var close and > 0
        )
            text = text[(close + 1)..];

        var query = text.IndexOfAny(['?', '#']);

        if (query >= 0)
            text = text[..query];

        if (text.StartsWith("//", StringComparison.Ordinal))
            text = text[2..];
        else if (text.IndexOf("://", StringComparison.Ordinal) is var scheme and > 0)
            text = text[(scheme + 3)..];
        else
        {
            yield return text.TrimStart('/');
            yield break;
        }

        // host/path: every path at the end of it
        var parts = text.Split('/', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 1; i < parts.Length; i++)
            yield return string.Join('/', parts[i..]);
    }

    private string TagsFile => Path.Combine(store.Root, ".media", "tags.json");

    private async Task<Dictionary<string, string[]>> ReadTagsAsync(CancellationToken ct)
    {
        if (!File.Exists(TagsFile))
            return [];

        try
        {
            var stream = File.OpenRead(TagsFile);
            await using var streamScope = stream.ConfigureAwait(false);

            return await JsonSerializer
                    .DeserializeAsync<Dictionary<string, string[]>>(stream, cancellationToken: ct)
                    .ConfigureAwait(false)
                ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogWarning(
                ex,
                "The media library's tags could not be read from {File}",
                TagsFile
            );

            return [];
        }
    }

    private Task SetTagsAsync(string id, IReadOnlyList<string> tags, CancellationToken ct) =>
        ChangeTagsAsync(
            all =>
            {
                string[] clean =
                [
                    .. tags.Select(x => x.Trim().ToLowerInvariant())
                        .Where(x => x.Length is > 0 and <= 40)
                        .Distinct(StringComparer.Ordinal)
                        .Take(30),
                ];

                if (clean.Length == 0)
                    all.Remove(id);
                else
                    all[id] = clean;
            },
            ct
        );

    private Task MoveTagsAsync(string from, string to, CancellationToken ct) =>
        ChangeTagsAsync(
            all =>
            {
                if (all.Remove(from, out var tags))
                    all[to] = tags;
            },
            ct
        );

    private async Task ChangeTagsAsync(
        Action<Dictionary<string, string[]>> change,
        CancellationToken ct
    )
    {
        await _tagsLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var all = await ReadTagsAsync(ct).ConfigureAwait(false);

            change(all);
            Directory.CreateDirectory(Path.GetDirectoryName(TagsFile)!);
            await File.WriteAllTextAsync(
                    TagsFile,
                    JsonSerializer.Serialize(
                        all.OrderBy(x => x.Key, StringComparer.Ordinal)
                            .ToDictionary(x => x.Key, x => x.Value)
                    ),
                    ct
                )
                .ConfigureAwait(false);
        }
        finally
        {
            _tagsLock.Release();
        }
    }

    private sealed record Entry(
        string Id,
        string Name,
        string Path,
        string Kind,
        string Source,
        string? SourceDetail,
        MediaValues Values,
        AssetBundleKind? StoreKind,
        string? StorePath,
        long Bytes,
        DateTime? Modified,
        (int Width, int Height)? Size,
        string[] Tags,
        Func<CancellationToken, Task<byte[]?>> Read
    )
    {
        public int? Width => Size?.Width;

        public int? Height => Size?.Height;
    }

    private sealed class Index(
        DateTimeOffset built,
        IReadOnlyList<Entry> entries,
        Dictionary<string, List<MediaUse>> uses
    )
    {
        private readonly Dictionary<string, Entry> _byId = entries
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        public DateTimeOffset Built { get; } = built;

        public IReadOnlyList<Entry> Entries { get; } = entries;

        public Entry? Find(string id) => _byId.GetValueOrDefault(id);

        public IReadOnlyList<MediaUse> UsesOf(string id) =>
            uses.TryGetValue(id, out var list) ? list : [];
    }
}

/// <summary>A media library search (<see cref="MediaLibrary.SearchAsync"/>): every filter optional, lists comma separated.</summary>
public sealed record MediaQuery(
    string? Text = null,
    string? Kind = null,
    string? Source = null,
    string? Shape = null,
    string? Used = null,
    int? MinWidth = null,
    int? MaxWidth = null,
    int? MinHeight = null,
    int? MaxHeight = null,
    string? Sort = null,
    bool Descending = false,
    int Page = 0,
    int Size = 60
);

/// <summary>What a rename, tag or delete came to: done (with the image, for an edit), no such image, refused with why, or refused because these show it.</summary>
public sealed record MediaEditOutcome(
    MediaImage? Image,
    bool Found,
    string? Error,
    IReadOnlyList<MediaUse>? UsedOn
)
{
    public static readonly MediaEditOutcome Missing = new(null, false, null, null);

    public static MediaEditOutcome Done(MediaImage? image) => new(image, true, null, null);

    public static MediaEditOutcome Refused(string why) => new(null, true, why, null);

    public static MediaEditOutcome InUse(IReadOnlyList<MediaUse> uses) =>
        new(
            null,
            true,
            $"It is shown on {uses.Count} {(uses.Count == 1 ? "place" : "places")}: change {(uses.Count == 1 ? "it" : "them")} first.",
            uses
        );
}
