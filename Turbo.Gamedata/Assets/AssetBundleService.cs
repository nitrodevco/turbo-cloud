using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Assets;
using Turbo.Assets.Conversion;
using Turbo.Assets.Nitro;
using Turbo.Database.Context;
using Turbo.Database.Entities.Assets;
using Turbo.Database.Extensions;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata.Assets;

/// <summary>
/// <see cref="IAssetBundleService"/> over the <c>asset_bundles</c> rows and the store's folder.
/// Whether a bundle is used is worked out from the furniture definitions and pet breeds as they
/// are when asked (<see cref="AssetBundleUsage"/>).
/// </summary>
internal sealed class AssetBundleService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<AssetBundleConfig> config,
    IAssetBundleStore store,
    AssetBundleChecks checks,
    TimeProvider time,
    ILogger<IAssetBundleService> logger
) : IAssetBundleService
{
    private const string SWF = ".swf";
    private const string HAB = ".hab";

    public long UploadMaxBytes =>
        Math.Max(1L, config.Value.UploadMaxMegabytes) * 1024 * 1024;

    public async Task<AssetOverview> GetOverviewAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var counts = await dbCtx
            .AssetBundles.AsNoTracking()
            .GroupBy(x => x.Kind)
            .Select(g => new
            {
                Kind = g.Key,
                Bundles = g.Count(x => x.Hash != null),
                Failed = g.Count(x => x.Error != null),
                Bytes = g.Sum(x => x.Hash != null ? x.Size : 0L),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var targets = await dbCtx
            .AssetPublishTargets.AsNoTracking()
            .CountAsync(ct)
            .ConfigureAwait(false);
        var found = (await checks.RunAsync(ct).ConfigureAwait(false))
            .Where(x => x.Count > 0)
            .ToList();

        return new AssetOverview
        {
            Directory = store.Root,
            Kinds =
            [
                .. Enum.GetValues<AssetBundleKind>()
                    .Select(kind =>
                        counts.FirstOrDefault(x => x.Kind == kind) is { } count
                            ? new AssetKindSummary
                            {
                                Kind = kind,
                                Bundles = count.Bundles,
                                Failed = count.Failed,
                                Bytes = count.Bytes,
                            }
                            : new AssetKindSummary
                            {
                                Kind = kind,
                                Bundles = 0,
                                Failed = 0,
                                Bytes = 0,
                            }
                    ),
            ],
            Errors = found.Count(x => x.Severity == AssetCheckSeverity.Error),
            Warnings = found.Count(x => x.Severity == AssetCheckSeverity.Warning),
            Targets = targets,
        };
    }

    public async Task<AssetBundlePage> ListAsync(
        AssetBundleKind? kind,
        string? query,
        AssetBundleStatusFilter status,
        int page,
        CancellationToken ct
    )
    {
        var pageSize = Math.Max(1, config.Value.PageSize);
        var skip = Math.Max(0, page) * pageSize;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var usage = await AssetBundleUsage.LoadAsync(dbCtx, ct).ConfigureAwait(false);
        var rows = Filter(dbCtx.AssetBundles.AsNoTracking(), kind, query, status);
        List<AssetBundleEntity> items;
        int total;

        if (status == AssetBundleStatusFilter.Unused)
        {
            // Whether a bundle is used is not a column, so the candidates are sorted out here.
            var unused = (
                await rows.Where(x =>
                        x.Kind == AssetBundleKind.Furniture || x.Kind == AssetBundleKind.Pet
                    )
                    .Select(x => new
                    {
                        x.Id,
                        x.Kind,
                        x.Name,
                        x.Ids,
                    })
                    .ToListAsync(ct)
                    .ConfigureAwait(false)
            )
                .Where(x => !usage.IsUsed(x.Kind, x.Name, x.Ids))
                .OrderBy(x => x.Kind)
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .ToList();
            var ids = unused.Skip(skip).Take(pageSize).Select(x => x.Id).ToList();
            var byId = (
                await dbCtx
                    .AssetBundles.AsNoTracking()
                    .Where(x => ids.Contains(x.Id))
                    .ToListAsync(ct)
                    .ConfigureAwait(false)
            ).ToDictionary(x => x.Id);

            total = unused.Count;
            items = [.. ids.Where(byId.ContainsKey).Select(x => byId[x])];
        }
        else
        {
            total = await rows.CountAsync(ct).ConfigureAwait(false);
            items = await rows.OrderBy(x => x.Kind)
                .ThenBy(x => x.Name)
                .ThenBy(x => x.Id)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        return new AssetBundlePage
        {
            Items = [.. items.Select(x => x.ToSnapshot(usage.IsUsed(x.Kind, x.Name, x.Ids)))],
            Total = total,
            PageSize = pageSize,
        };
    }

    public async Task<AssetBundleDetail?> GetAsync(
        AssetBundleKind kind,
        string name,
        CancellationToken ct
    )
    {
        if (!store.IsValidName(name))
            return null;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await FindAsync(dbCtx.AssetBundles.AsNoTracking(), kind, name, ct)
            .ConfigureAwait(false);

        if (row is null)
            return null;

        return new AssetBundleDetail
        {
            Bundle = row.ToSnapshot(
                await IsUsedAsync(dbCtx, row, ct).ConfigureAwait(false)
            ),
            Path = store.PathOf(kind, name),
            Files = row.Hash is null ? [] : FilesOf(kind, name),
        };
    }

    public async Task<string?> GetFilePathAsync(
        AssetBundleKind kind,
        string name,
        CancellationToken ct
    )
    {
        if (!store.IsValidName(name))
            return null;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var hasFile = await dbCtx
            .AssetBundles.AsNoTracking()
            .AnyAsync(x => x.Kind == kind && x.Name == name && x.Hash != null, ct)
            .ConfigureAwait(false);
        var path = store.FullPathOf(kind, name);

        return hasFile && File.Exists(path) ? path : null;
    }

    public async Task<bool> DeleteAsync(
        AssetBundleKind kind,
        string name,
        PlayerId player,
        CancellationToken ct
    )
    {
        if (!store.IsValidName(name))
            return false;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await FindAsync(dbCtx.AssetBundles, kind, name, ct).ConfigureAwait(false);

        if (row is null)
            return false;

        dbCtx.AssetBundles.Remove(row);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        store.Delete(kind, name);

        logger.LogInformation(
            "Player {PlayerId} deleted the asset bundle {Kind} {Name}",
            player,
            kind,
            name
        );

        return true;
    }

    public async Task<AssetBundleSnapshot> UploadAsync(
        AssetBundleKind kind,
        string? name,
        string fileName,
        byte[] data,
        PlayerId player,
        CancellationToken ct
    )
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (extension is not (SWF or HAB or AssetBundleStore.EXTENSION))
            throw new ArgumentException("A bundle is uploaded as a .swf, .hab or .nitro file.");

        if (data.Length == 0)
            throw new ArgumentException("The file is empty.");

        if (data.LongLength > UploadMaxBytes)
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The file is larger than {config.Value.UploadMaxMegabytes} MB."
                )
            );

        name = string.IsNullOrWhiteSpace(name)
            ? Path.GetFileNameWithoutExtension(fileName)
            : name.Trim();

        if (!store.IsValidName(name))
            throw new ArgumentException(
                $"\"{name}\" can't be a bundle's name: use letters, digits, _, - and ."
            );

        var bundle = ToBundle(kind, name, extension, data);
        var hash = await store.WriteAsync(kind, name, bundle, ct).ConfigureAwait(false);

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await FindAsync(dbCtx.AssetBundles, kind, name, ct).ConfigureAwait(false);

        if (row is null)
        {
            row = new AssetBundleEntity
            {
                Kind = kind,
                Name = name,
                Source = AssetBundleSource.Upload,
            };
            dbCtx.AssetBundles.Add(row);
        }

        // An upload keeps the ids a Habbo library of the name had: they still load it.
        row.Source = AssetBundleSource.Upload;
        row.Revision = null;
        row.Hash = hash;
        row.Size = bundle.LongLength;
        row.Error = null;
        row.Retry = false;
        row.UpdatedAt = time.GetUtcNow().UtcDateTime;

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Player {PlayerId} uploaded the asset bundle {Kind} {Name} ({Hash})",
            player,
            kind,
            name,
            hash
        );

        return row.ToSnapshot(await IsUsedAsync(dbCtx, row, ct).ConfigureAwait(false));
    }

    public Task<ImmutableArray<AssetCheckSnapshot>> GetChecksAsync(CancellationToken ct) =>
        checks.RunAsync(ct);

    /// <summary>
    /// The asset type a library of this kind converts as: clothing as <c>figure</c>, effects as
    /// <c>fx</c>, furniture and pets as they are.
    /// </summary>
    public static string? AssetTypeOf(AssetBundleKind kind) =>
        kind switch
        {
            AssetBundleKind.Figure => AssetDataFilter.FIGURE,
            AssetBundleKind.Effect => AssetDataFilter.EFFECT,
            _ => null,
        };

    /// <summary>The upload as the bundle to keep: a library converted, a <c>.nitro</c> as it is once it reads.</summary>
    private static byte[] ToBundle(
        AssetBundleKind kind,
        string name,
        string extension,
        byte[] data
    )
    {
        try
        {
            if (extension == AssetBundleStore.EXTENSION)
            {
                NitroBundle.Read(data);

                return data;
            }

            return NitroConverter.Convert(data, name, AssetTypeOf(kind)).Write();
        }
        catch (AssetFormatException ex)
        {
            throw new ArgumentException($"The file can't be taken: {ex.Message}", ex);
        }
    }

    private static IQueryable<AssetBundleEntity> Filter(
        IQueryable<AssetBundleEntity> rows,
        AssetBundleKind? kind,
        string? query,
        AssetBundleStatusFilter status
    )
    {
        if (kind is { } only)
            rows = rows.Where(x => x.Kind == only);

        var text = query?.Trim();

        if (!string.IsNullOrEmpty(text))
        {
            if (
                int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            )
            {
                // Wrapped in separators, an id matches whole wherever it is in the list.
                var separator = AssetBundleIds.SEPARATOR.ToString();
                var token = separator + id.ToString(CultureInfo.InvariantCulture) + separator;

                rows = rows.Where(x =>
                    x.Name.Contains(text)
                    || (x.Ids != null && (separator + x.Ids + separator).Contains(token))
                );
            }
            else
            {
                rows = rows.Where(x => x.Name.Contains(text));
            }
        }

        return status switch
        {
            AssetBundleStatusFilter.Ok => rows.Where(x => x.Hash != null && x.Error == null),
            AssetBundleStatusFilter.Failed => rows.Where(x => x.Error != null),
            _ => rows,
        };
    }

    private static Task<AssetBundleEntity?> FindAsync(
        IQueryable<AssetBundleEntity> rows,
        AssetBundleKind kind,
        string name,
        CancellationToken ct
    ) => rows.FirstOrDefaultAsync(x => x.Kind == kind && x.Name == name, ct);

    /// <summary>Whether the hotel names this one bundle, asked of just what it needs.</summary>
    private static async Task<bool> IsUsedAsync(
        TurboDbContext dbCtx,
        AssetBundleEntity row,
        CancellationToken ct
    )
    {
        switch (row.Kind)
        {
            case AssetBundleKind.Furniture:
                var colours = $"{row.Name}*";

                return await dbCtx
                    .FurnitureDefinitions.AsNoTracking()
                    .AnyAsync(x => x.Name == row.Name || x.Name.StartsWith(colours), ct)
                    .ConfigureAwait(false);
            case AssetBundleKind.Pet:
                var types = AssetBundleIds.Parse(row.Ids).ToList();

                return types.Count > 0
                    && await dbCtx
                        .PetBreeds.AsNoTracking()
                        .AnyAsync(x => types.Contains(x.TypeId), ct)
                        .ConfigureAwait(false);
            default:
                return true;
        }
    }

    /// <summary>What a bundle's zip holds; nothing when its file is gone or unreadable.</summary>
    private ImmutableArray<AssetBundleEntry> FilesOf(AssetBundleKind kind, string name)
    {
        var path = store.FullPathOf(kind, name);

        if (!File.Exists(path))
            return [];

        try
        {
            using var zip = ZipFile.OpenRead(path);

            return
            [
                .. zip
                    .Entries.Where(x => !x.FullName.EndsWith('/'))
                    .Select(x => new AssetBundleEntry { Name = x.FullName, Size = x.Length }),
            ];
        }
        catch (InvalidDataException ex)
        {
            logger.LogWarning(ex, "The asset bundle {Kind} {Name} is not a readable zip", kind, name);

            return [];
        }
    }
}
