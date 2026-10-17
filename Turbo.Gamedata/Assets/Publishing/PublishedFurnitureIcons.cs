using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
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
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Habbo;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata.Assets.Publishing;

/// <summary>
/// The furniture icons a publish sends, drawn as nitro-studio draws them (<see cref="FurnitureIcon"/>)
/// into <c>images/furni-icons</c>: <c>&lt;asset&gt;_icon.png</c> for each furniture bundle, and
/// <c>&lt;asset&gt;_&lt;N&gt;_icon.png</c> for each colour <c>classname*N</c> the hotel's furniture
/// has - the client asks for them by <c>asset.urls.icons.furni</c>. An icon is drawn again when its
/// bundle changed (its row keeps the bundle's hash as its revision); one no bundle or colour wants
/// any more goes. A bundle that draws no icon has Habbo's, as the Studio takes it.
/// </summary>
internal sealed class PublishedFurnitureIcons(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IAssetBundleStore store,
    HabboGamedataClient habbo,
    IOptions<AssetBundleConfig> config,
    IOptions<GamedataConfig> gamedataConfig,
    ILogger<PublishedFurnitureIcons> logger
)
{
    private const int SAVE_BATCH = 200;

    /// <summary>A furniture icon's file: the bundle's, or its colour's.</summary>
    public static string FileOf(string bundle, int? color) =>
        color is { } n
            ? $"{bundle}_{n.ToString(CultureInfo.InvariantCulture)}_icon.png"
            : $"{bundle}_icon.png";

    /// <summary>What it did, in words for the job's log.</summary>
    public async Task<string> BuildAsync(IAssetJobProgress progress, CancellationToken ct)
    {
        var (bundles, colors, icons) = await LoadAsync(ct).ConfigureAwait(false);
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        var todo = new List<(AssetBundleEntity Bundle, List<int?> Colors)>();

        foreach (var bundle in bundles)
        {
            List<int?> files =
            [
                null,
                .. colors.GetValueOrDefault(bundle.Name, []).Order().Cast<int?>(),
            ];
            var stale = false;

            foreach (var color in files)
            {
                var file = FileOf(bundle.Name, color);

                wanted.Add(file);
                stale |=
                    !icons.TryGetValue(file, out var icon)
                    || icon.Revision != bundle.Hash
                    || !File.Exists(store.FullPathOf(AssetBundleKind.FurniIcon, file));
            }

            if (stale)
                todo.Add((bundle, files));
        }

        progress.Step("Furniture icons", todo.Count);

        var drawn =
            new ConcurrentQueue<(
                string File,
                string Revision,
                AssetBundleSource Source,
                string Hash,
                long Size
            )>();
        var failed = 0;

        await Parallel
            .ForEachAsync(
                todo,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, config.Value.ConvertConcurrency),
                    CancellationToken = ct,
                },
                async (item, token) =>
                {
                    try
                    {
                        foreach (
                            var icon in await IconsOfAsync(item.Bundle, item.Colors, token)
                                .ConfigureAwait(false)
                        )
                            drawn.Enqueue(icon);

                        progress.Advance();
                    }
                    catch (Exception ex)
                        when (ex is IOException or AssetFormatException or InvalidDataException)
                    {
                        Interlocked.Increment(ref failed);
                        logger.LogWarning(
                            ex,
                            "The furniture icons of {Bundle} could not be drawn",
                            item.Bundle.Name
                        );
                        progress.Log(
                            $"{item.Bundle.Name}: its icon could not be drawn ({ex.Message})."
                        );
                        // Not a file the publish failed to send: the bundle goes all the same.
                        progress.Advance();
                    }
                }
            )
            .ConfigureAwait(false);

        var removed = icons.Keys.Where(x => !wanted.Contains(x)).ToList();

        await SaveAsync([.. drawn], removed).ConfigureAwait(false);

        foreach (var file in removed)
            store.Delete(AssetBundleKind.FurniIcon, file);

        return $"Furniture icons: {drawn.Count} drawn for {todo.Count - failed} bundles, {removed.Count} removed"
            + (failed > 0 ? $", {failed} bundles could not be drawn." : ".");
    }

    /// <summary>A bundle's icons, written to the store: drawn from it, else Habbo's.</summary>
    private async Task<List<(string, string, AssetBundleSource, string, long)>> IconsOfAsync(
        AssetBundleEntity bundle,
        List<int?> colors,
        CancellationToken ct
    )
    {
        var file = NitroBundle.Read(
            await File.ReadAllBytesAsync(
                    store.FullPathOf(AssetBundleKind.Furniture, bundle.Name),
                    ct
                )
                .ConfigureAwait(false)
        );
        var icons = new List<(string, string, AssetBundleSource, string, long)>();
        byte[]? plain = null;

        foreach (var color in colors)
        {
            var name = FileOf(bundle.Name, color);
            // The colour 0 a furnidata lists is the plain icon.
            var png = color is null or 0
                ? plain ??= FurnitureIcon.Draw(file, bundle.Name)?.EncodePng()
                : FurnitureIcon.Draw(file, bundle.Name, color.Value)?.EncodePng();
            var source = AssetBundleSource.Built;

            if (png is null)
            {
                png = await HabboIconAsync(bundle, color, ct).ConfigureAwait(false);
                source = AssetBundleSource.Habbo;
            }

            if (png is null)
                continue;

            var hash = await store
                .WriteAsync(AssetBundleKind.FurniIcon, name, png, ct)
                .ConfigureAwait(false);

            icons.Add((name, bundle.Hash!, source, hash, png.LongLength));
        }

        return icons;
    }

    /// <summary>Habbo's icon of a bundle of its own that has none to draw; null when it has none either.</summary>
    private async Task<byte[]?> HabboIconAsync(
        AssetBundleEntity bundle,
        int? color,
        CancellationToken ct
    )
    {
        if (bundle.Source != AssetBundleSource.Habbo || string.IsNullOrEmpty(bundle.Revision))
            return null;

        var className = FurnitureAssetNames.AssetNameOf(bundle.Name);
        var url = config
            .Value.FurnitureIconUrl.Replace(
                "{domain}",
                gamedataConfig.Value.HabboDomain,
                StringComparison.Ordinal
            )
            .Replace("{revision}", bundle.Revision, StringComparison.Ordinal)
            .Replace(
                "{name}",
                Uri.EscapeDataString(
                    color is { } n
                        ? $"{className}_{n.ToString(CultureInfo.InvariantCulture)}"
                        : className
                ),
                StringComparison.Ordinal
            );

        try
        {
            var png = await habbo.GetFileAsync(url, ct).ConfigureAwait(false);

            return png is { Length: > 8 } && png[0] == 0x89 && png[1] == (byte)'P' ? png : null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "Habbo's icon of {Bundle} could not be downloaded from {Url}",
                bundle.Name,
                url
            );

            return null;
        }
    }

    private async Task<(
        List<AssetBundleEntity> Bundles,
        Dictionary<string, HashSet<int>> Colors,
        Dictionary<string, AssetBundleEntity> Icons
    )> LoadAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var bundles = await dbCtx
            .AssetBundles.AsNoTracking()
            .Where(x => x.Kind == AssetBundleKind.Furniture && x.Hash != null)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var names = await dbCtx
            .FurnitureDefinitions.AsNoTracking()
            .Where(x => x.Name.Contains("*"))
            .Select(x => x.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var icons = await dbCtx
            .AssetBundles.AsNoTracking()
            .Where(x => x.Kind == AssetBundleKind.FurniIcon)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var colors = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

        // A colour is digits after the star, as the client takes a furnidata colour variant.
        foreach (var name in names)
        {
            var colour = name[(name.IndexOf('*', StringComparison.Ordinal) + 1)..];

            if (
                colour.Length == 0
                || !colour.All(char.IsAsciiDigit)
                || !int.TryParse(colour, CultureInfo.InvariantCulture, out var id)
            )
                continue;

            var bundle = FurnitureAssetNames.BundleNameOf(name);

            if (!colors.TryGetValue(bundle, out var ids))
                colors[bundle] = ids = [];

            ids.Add(id);
        }

        return (
            [.. bundles.Where(x => store.IsValidName(x.Name))],
            colors,
            icons.ToDictionary(x => x.Name, StringComparer.Ordinal)
        );
    }

    private async Task SaveAsync(
        List<(
            string File,
            string Revision,
            AssetBundleSource Source,
            string Hash,
            long Size
        )> drawn,
        List<string> removed
    )
    {
        var now = DateTime.UtcNow;

        foreach (var chunk in drawn.Chunk(SAVE_BATCH))
        {
            var dbCtx = await dbCtxFactory
                .CreateDbContextAsync(CancellationToken.None)
                .ConfigureAwait(false);
            await using var dbCtxScope = dbCtx.ConfigureAwait(false);

            var names = chunk.Select(x => x.File).ToList();
            var rows = await dbCtx
                .AssetBundles.Where(x =>
                    x.Kind == AssetBundleKind.FurniIcon && names.Contains(x.Name)
                )
                .ToDictionaryAsync(x => x.Name, StringComparer.Ordinal, CancellationToken.None)
                .ConfigureAwait(false);

            foreach (var icon in chunk)
            {
                if (!rows.TryGetValue(icon.File, out var row))
                {
                    row = new AssetBundleEntity
                    {
                        Kind = AssetBundleKind.FurniIcon,
                        Name = icon.File,
                        Source = icon.Source,
                    };
                    dbCtx.AssetBundles.Add(row);
                    rows[icon.File] = row;
                }

                row.Source = icon.Source;
                row.Revision = icon.Revision;
                row.Hash = icon.Hash;
                row.Size = icon.Size;
                row.Error = null;
                row.UpdatedAt = now;
            }

            await dbCtx.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }

        foreach (var chunk in removed.Chunk(SAVE_BATCH))
        {
            var dbCtx = await dbCtxFactory
                .CreateDbContextAsync(CancellationToken.None)
                .ConfigureAwait(false);
            await using var dbCtxScope = dbCtx.ConfigureAwait(false);

            await dbCtx
                .AssetBundles.Where(x =>
                    x.Kind == AssetBundleKind.FurniIcon && chunk.Contains(x.Name)
                )
                .ExecuteDeleteAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }
    }
}
