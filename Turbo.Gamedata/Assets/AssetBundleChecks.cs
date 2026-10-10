using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Gamedata.Assets;

/// <summary>
/// The bundles checked against the hotel: furniture definitions with nothing to draw them, rows
/// whose file is gone, libraries that failed, effects the catalog sells and pets with breeds that
/// no bundle carries, and furniture bundles nothing names. Each lists how many it found and up to
/// <see cref="AssetBundleConfig.CheckSampleLimit"/> of them.
/// </summary>
internal sealed class AssetBundleChecks(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<AssetBundleConfig> config,
    IAssetBundleStore store
)
{
    public const string FURNITURE_MISSING = "furniture-missing";
    public const string FILE_MISSING = "file-missing";
    public const string FAILED = "failed";
    public const string EFFECTS_MISSING = "effects-missing";
    public const string PETS_MISSING = "pets-missing";
    public const string FURNITURE_UNUSED = "furniture-unused";

    public async Task<ImmutableArray<AssetCheckSnapshot>> RunAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .AssetBundles.AsNoTracking()
            .Select(x => new Row(x.Kind, x.Name, x.Hash != null, x.Error, x.Ids))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        // By kind and name, so each check lists what it found in that order.
        var bundles = rows.OrderBy(x => x.Kind)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
        var usage = await AssetBundleUsage.LoadAsync(dbCtx, ct).ConfigureAwait(false);
        var effectParams = await dbCtx
            .CatalogProducts.AsNoTracking()
            .Where(x => x.ProductType == ProductType.Effect && x.ExtraParam != null)
            .Select(x => x.ExtraParam)
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var withFile = bundles.Where(x => x.HasFile).ToList();
        var furnitureWithFile = withFile
            .Where(x => x.Kind == AssetBundleKind.Furniture)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);
        var effectIds = IdsOf(withFile, AssetBundleKind.Effect);
        var petTypes = IdsOf(withFile, AssetBundleKind.Pet);
        var soldEffects = effectParams
            .Select(x => EffectProducts.TryGetEffectId(x, out var id) ? id : 0)
            .Where(x => x > 0)
            .ToHashSet();

        return
        [
            Check(
                FURNITURE_MISSING,
                AssetCheckSeverity.Error,
                "Furniture without a bundle",
                "Furniture definitions whose asset has no bundle: the client draws a placeholder.",
                usage
                    .Furniture.Where(x => x.Length > 0 && !furnitureWithFile.Contains(x))
                    .Order(StringComparer.Ordinal)
            ),
            Check(
                FILE_MISSING,
                AssetCheckSeverity.Error,
                "Bundles whose file is gone",
                "Their row says they have a file that the bundle folder does not have. Delete them, and a sync takes Habbo's again.",
                withFile
                    .Where(x =>
                        !store.IsValidName(x.Name) || !File.Exists(store.FullPathOf(x.Kind, x.Name))
                    )
                    .Select(x => Label(x.Kind, x.Name))
            ),
            Check(
                FAILED,
                AssetCheckSeverity.Warning,
                "Libraries that failed",
                "Libraries that could not be downloaded or converted, with why.",
                bundles
                    .Where(x => x.Error is not null)
                    .Select(x => $"{Label(x.Kind, x.Name)}: {x.Error}"),
                status: AssetBundleStatusFilter.Failed
            ),
            Check(
                EFFECTS_MISSING,
                AssetCheckSeverity.Warning,
                "Effects without a bundle",
                "Effects the catalog sells that no effect bundle carries: the avatar wears nothing.",
                soldEffects
                    .Where(x => !effectIds.Contains(x))
                    .Order()
                    .Select(x => $"effect {x.ToString(CultureInfo.InvariantCulture)}")
            ),
            Check(
                PETS_MISSING,
                AssetCheckSeverity.Warning,
                "Pets without a bundle",
                "Pet types with breeds that no pet bundle carries: the pet can't be drawn.",
                usage
                    .PetTypes.Where(x => !petTypes.Contains(x))
                    .Order()
                    .Select(x => $"pet type {x.ToString(CultureInfo.InvariantCulture)}")
            ),
            Check(
                FURNITURE_UNUSED,
                AssetCheckSeverity.Warning,
                "Furniture bundles nothing uses",
                "Furniture bundles no definition names. They do no harm; they are only kept and published.",
                bundles
                    .Where(x =>
                        x.Kind == AssetBundleKind.Furniture && !usage.IsUsed(x.Kind, x.Name, x.Ids)
                    )
                    .Select(x => x.Name),
                AssetBundleKind.Furniture,
                AssetBundleStatusFilter.Unused
            ),
        ];
    }

    private AssetCheckSnapshot Check(
        string id,
        AssetCheckSeverity severity,
        string title,
        string detail,
        IEnumerable<string> found,
        AssetBundleKind? kind = null,
        AssetBundleStatusFilter? status = null
    )
    {
        var all = found.ToList();

        return new AssetCheckSnapshot
        {
            Id = id,
            Severity = severity,
            Title = title,
            Detail = detail,
            Count = all.Count,
            Samples = [.. all.Take(Math.Max(0, config.Value.CheckSampleLimit))],
            Kind = kind,
            Status = status,
        };
    }

    private static HashSet<int> IdsOf(IEnumerable<Row> rows, AssetBundleKind kind) =>
        [.. rows.Where(x => x.Kind == kind).SelectMany(x => AssetBundleIds.Parse(x.Ids))];

    private static string Label(AssetBundleKind kind, string name) =>
        $"{kind.ToString().ToLowerInvariant()}/{name}";

    private sealed record Row(
        AssetBundleKind Kind,
        string Name,
        bool HasFile,
        string? Error,
        string? Ids
    );
}
