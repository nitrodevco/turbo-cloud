using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    public string Root { get; } =
        Path.GetFullPath(config.Value.Directory, environment.ContentRootPath);

    public string PathOf(AssetBundleKind kind, string name) =>
        $"{FolderOf(kind)}/{name}{EXTENSION}";

    public string FullPathOf(AssetBundleKind kind, string name)
    {
        if (!IsValidName(name))
            throw new ArgumentException($"\"{name}\" can't be a bundle's name.", nameof(name));

        return Path.Combine(Root, PathOf(kind, name));
    }

    public bool IsValidName(string name) =>
        name.Length is > 0 and <= Database.Entities.Assets.AssetBundleEntity.NAME_MAX_LENGTH
        && NamePattern().IsMatch(name)
        && name is not "." and not "..";

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

        return HashOf(bundle);
    }

    public void Delete(AssetBundleKind kind, string name)
    {
        var path = FullPathOf(kind, name);

        if (File.Exists(path))
            File.Delete(path);
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

    /// <summary>A bundle's SHA-1, in lowercase hex.</summary>
    public static string HashOf(byte[] bundle) => Convert.ToHexStringLower(SHA1.HashData(bundle));

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex NamePattern();
}
