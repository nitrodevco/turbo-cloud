using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Gamedata.Assets;

/// <summary>
/// <see cref="IAssetBundleStore"/> over the configured folder: a bundle is written to a file beside
/// its place and moved over it, so a reader (or a publish) never sees half of one.
/// </summary>
internal sealed partial class AssetBundleStore(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<AssetBundleConfig> config,
    IHostEnvironment environment
) : IAssetBundleStore
{
    public const string EXTENSION = ".nitro";

    /// <summary>The folder each kind is kept and served in, as nitro's own asset host lays them out.</summary>
    public static string FolderOf(AssetBundleKind kind) =>
        kind switch
        {
            AssetBundleKind.Furniture => "bundled/furniture",
            AssetBundleKind.Effect => "bundled/effects",
            AssetBundleKind.Pet => "bundled/pet",
            AssetBundleKind.Figure => "bundled/figures",
            AssetBundleKind.Template => "bundled/templates",
            AssetBundleKind.Room => "bundled/room",
            AssetBundleKind.FurniIcon => "images/furni-icons",
            AssetBundleKind.CatalogIcon => "images/catalog-icons",
            AssetBundleKind.Badge => "images/badges",
            AssetBundleKind.CatalogImage => "images/catalog",
            AssetBundleKind.Sound => "sounds",
            AssetBundleKind.LibraryImage => "c_images",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    public string Root { get; } =
        Path.GetFullPath(config.Value.Directory, environment.ContentRootPath);

    public string PathOf(AssetBundleKind kind, string name) =>
        $"{FolderOf(kind)}/{name}{(kind.IsBundle() ? EXTENSION : "")}";

    public string FullPathOf(AssetBundleKind kind, string name)
    {
        if (!IsValidName(kind, name))
            throw new ArgumentException($"\"{name}\" can't be a bundle's name.", nameof(name));

        return Path.Combine(Root, PathOf(kind, name));
    }

    public bool IsValidName(string name) =>
        name.Length is > 0 and <= Database.Entities.Assets.AssetBundleEntity.NAME_MAX_LENGTH
        && NamePattern().IsMatch(name)
        && name is not "." and not "..";

    public bool IsValidName(AssetBundleKind kind, string name) =>
        kind == AssetBundleKind.LibraryImage
            ? name.Length <= Database.Entities.Assets.AssetBundleEntity.NAME_MAX_LENGTH
                && name.Split('/').All(IsValidName)
            : IsValidName(name);

    public async Task<string> WriteAsync(
        AssetBundleKind kind,
        string name,
        byte[] bundle,
        CancellationToken ct
    )
    {
        var path = FullPathOf(kind, name);
        var temp = $"{path}.{Guid.NewGuid():N}.part";

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            await File.WriteAllBytesAsync(temp, bundle, ct).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }

        return GamedataBytes.Hash(bundle);
    }

    public void Delete(AssetBundleKind kind, string name)
    {
        var path = FullPathOf(kind, name);

        if (File.Exists(path))
            File.Delete(path);
    }

    public IReadOnlySet<string> NamesOnDisk(AssetBundleKind kind)
    {
        var folder = Path.Combine(Root, FolderOf(kind));
        var extension = kind.IsBundle() ? EXTENSION : "";
        // As the file system tells names apart: where it ignores case, so does a lookup here.
        var names = new HashSet<string>(
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal
        );

        if (!Directory.Exists(folder))
            return names;

        // The image library's pictures are in folders of their own under it, named with them.
        var nested = kind == AssetBundleKind.LibraryImage;

        foreach (
            var path in Directory.EnumerateFiles(
                folder,
                "*",
                nested ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly
            )
        )
        {
            var file = nested
                ? Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/')
                : Path.GetFileName(path);

            if (extension.Length == 0)
                names.Add(file);
            else if (file.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                names.Add(file[..^extension.Length]);
        }

        return names;
    }

    public async Task<IReadOnlyList<AssetBundleFile>> ListFilesAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .AssetBundles.AsNoTracking()
            .Where(x => x.Hash != null)
            .Select(x => new
            {
                x.Kind,
                x.Name,
                x.Hash,
                x.Size,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(x => new AssetBundleFile(
                    x.Kind,
                    x.Name,
                    PathOf(x.Kind, x.Name),
                    x.Hash!,
                    x.Size
                ))
                .OrderBy(x => x.Path, StringComparer.Ordinal),
        ];
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex NamePattern();
}
