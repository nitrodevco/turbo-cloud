using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Gamedata;
using Turbo.Gamedata.Assets;
using Turbo.Gamedata.Habbo;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Rooms.Providers;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// Every floor and wall definition with its tags (<see cref="FurnitureTag"/>): matched to Habbo's
/// newest release as an import matches it (kind and classname, with what its asset file said),
/// to the bundle of its asset, and to the catalogs players see.
/// <para>
/// Working it out reads every definition and every item Habbo's release has, so it is kept and
/// worked out again only once something it is made from has changed: a definition (every change
/// the gamedata makes is a change set; others add or remove rows), a bundle, an asset file read,
/// Habbo's release, a catalog published, or the logics the server registers.
/// </para>
/// </summary>
internal sealed class FurnitureStatusIndex(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    HabboReleaseItems habboItems,
    HabboFurnitureFiles furnitureFiles,
    FurnitureOfferCatalog offers,
    IRoomObjectLogicProvider logics,
    ILogger<FurnitureStatusIndex>? logger = null
)
{
    private readonly SemaphoreSlim _building = new(1, 1);
    private Built? _built;

    /// <summary>Every definition, by classname, with its tags.</summary>
    public async Task<IReadOnlyList<FurnitureListItem>> GetAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var stamps = await offers.GetAsync(ct).ConfigureAwait(false);
        var latest = await habboItems.GetLatestAsync(ct).ConfigureAwait(false);
        var key = await KeyAsync(dbCtx, stamps, latest, logics.Version, ct).ConfigureAwait(false);

        if (_built is { } built && built.Key == key)
            return built.Items;

        await _building.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (_built is { } done && done.Key == key)
                return done.Items;

            var items = await BuildAsync(dbCtx, stamps, latest, ct).ConfigureAwait(false);

            _built = new Built(key, items);

            return items;
        }
        finally
        {
            _building.Release();
        }
    }

    private async Task<List<FurnitureListItem>> BuildAsync(
        TurboDbContext dbCtx,
        FurnitureOfferCatalog.Stamps stamps,
        HabboReleaseItems.Loaded? latest,
        CancellationToken ct
    )
    {
        var definitions = await dbCtx
            .FurnitureDefinitions.AsNoTracking()
            .Where(x => x.ProductType == ProductType.Floor || x.ProductType == ProductType.Wall)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var bases = await dbCtx
            .HabboFurniture.AsNoTracking()
            .Select(x => new
            {
                x.ProductType,
                x.ClassName,
                x.Data,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var baseData = bases
            .GroupBy(x => (x.ProductType, x.ClassName))
            .ToDictionary(g => g.Key, g => g.First().Data);
        var bundles = (
            await dbCtx
                .AssetBundles.AsNoTracking()
                .Where(x => x.Kind == AssetBundleKind.Furniture)
                .Select(x => new Bundle(
                    x.Name,
                    x.Hash != null,
                    x.Error,
                    x.Source,
                    x.Revision,
                    x.Size
                ))
                .ToListAsync(ct)
                .ConfigureAwait(false)
        ).ToDictionary(x => x.Name, StringComparer.Ordinal);
        var files = await furnitureFiles.LoadAsync(ct).ConfigureAwait(false);
        var habboRevisions = AssetRevisions(latest);
        var hasPosters = bundles.Values.Any(x => x.HasFile && FurnitureAssetNames.IsPoster(x.Name));
        var registered = logics.LogicTypes.ToHashSet(StringComparer.Ordinal);
        var clientLogics = ClientLogics(files);

        return
        [
            .. definitions
                .Select(definition =>
                {
                    JsonObject? habbo = null;

                    if (
                        latest?.Items.TryGetValue(
                            (definition.ProductType, definition.Name),
                            out var raw
                        ) == true
                    )
                        habbo = HabboFurnitureFiles.Enrich(raw, files);

                    var @base =
                        habbo is not null
                        && baseData.TryGetValue(
                            (definition.ProductType, definition.Name),
                            out var data
                        )
                            ? JsonNode.Parse(data) as JsonObject
                            : null;
                    var asset = FurnitureAssetNames.BundleNameOf(definition.Name);
                    var bundle = bundles.GetValueOrDefault(asset) is { HasFile: true } kept
                        ? kept
                        : null;
                    var matching = MatchingLogic(
                        definition,
                        clientLogics.GetValueOrDefault(
                            HabboFurnitureFiles.AssetName(definition.Name)
                        ),
                        registered
                    );

                    return new FurnitureListItem
                    {
                        Id = definition.Id,
                        ProductType = definition.ProductType,
                        ClassName = definition.Name,
                        Name = definition.PublicName,
                        SpriteId = definition.SpriteId,
                        FurniLine = definition.FurniLine,
                        Category = definition.ClientCategory,
                        AssetName = asset,
                        BundleRevision = bundle?.Revision,
                        BundleSize = bundle?.Size,
                        BundleSource = bundle?.Source,
                        Logic = definition.Logic,
                        MatchingLogic = matching,
                        Tags = TagsOf(
                            definition,
                            habbo,
                            @base,
                            bundles.GetValueOrDefault(asset),
                            habboRevisions.TryGetValue(asset, out var revision) ? revision : null,
                            asset == FurnitureAssetNames.POSTER && hasPosters,
                            stamps.Sold.Contains(definition.Id),
                            IsMismatch(definition.Logic, matching, registered),
                            IsVendingConfigurationMissing(definition)
                        ),
                    };
                })
                .OrderBy(x => x.ClassName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id),
        ];
    }

    /// <summary>
    /// A definition's tags. Of the fields where its value is not Habbo's, those an import would
    /// take are Habbo's update; the rest are the hotel's own values.
    /// </summary>
    private static ImmutableArray<FurnitureTag> TagsOf(
        FurnitureDefinitionEntity definition,
        JsonObject? habbo,
        JsonObject? @base,
        Bundle? bundle,
        string? habboRevision,
        bool drawnFromPosters,
        bool sold,
        bool logicMismatch,
        bool vendingConfigurationMissing
    )
    {
        var tags = ImmutableArray.CreateBuilder<FurnitureTag>();

        if (habbo is null)
        {
            tags.Add(FurnitureTag.HotelOwn);
        }
        else
        {
            var taken = FurnitureMerge
                .Compare(definition, habbo, @base)
                .Where(x => !x.Kept)
                .Select(x => x.Field)
                .ToHashSet(StringComparer.Ordinal);
            var isWall = definition.ProductType == ProductType.Wall;
            var edited = FurnitureFields.All.Any(field =>
                field.AppliesTo(isWall)
                && field.IsIn(habbo)
                && !taken.Contains(field.Key)
                && !FurnitureField.Same(field.Read(definition), field.Normalize(habbo[field.Key]))
            );

            if (taken.Count > 0)
                tags.Add(FurnitureTag.HabboUpdate);

            if (edited)
                tags.Add(FurnitureTag.Edited);
        }

        if (bundle is not { HasFile: true } && !drawnFromPosters)
            tags.Add(FurnitureTag.NoBundle);

        if (bundle?.Error is not null)
            tags.Add(FurnitureTag.BundleFailed);
        else if (
            bundle is { HasFile: true, Source: AssetBundleSource.Habbo }
            && habboRevision is not null
            && bundle.Revision != habboRevision
        )
            tags.Add(FurnitureTag.BundleOutdated);

        if (!sold)
            tags.Add(FurnitureTag.NotSold);

        if (logicMismatch)
            tags.Add(FurnitureTag.LogicMismatch);

        if (vendingConfigurationMissing)
            tags.Add(FurnitureTag.VendingConfigMissing);

        return tags.ToImmutable();
    }

    private bool IsVendingConfigurationMissing(FurnitureDefinitionEntity definition)
    {
        if (definition.ProductType != ProductType.Floor || definition.Logic != "vending_machine")
            return false;

        var data = FurnitureExtraDataSections.ReadDefinition<VendingMachineData>(
            definition.ExtraData,
            VendingMachineData.SECTION,
            logger ?? NullLogger<FurnitureStatusIndex>.Instance
        );
        return data is null || data.HandItems.IsDefaultOrEmpty || data.HandItems.Any(id => id <= 0);
    }

    /// <summary>
    /// The logic the server has for a definition as a new one would get it: registered under its
    /// classname, else the one its asset file's client logic names, else the one its client
    /// category stands for; null when that is the default.
    /// </summary>
    public static string? MatchingLogic(
        FurnitureDefinitionEntity definition,
        string? clientLogic,
        IReadOnlySet<string> registered
    )
    {
        var logic = FurnitureDrafts.LogicOf(
            definition.ProductType,
            definition.Name,
            clientLogic,
            definition.ClientCategory,
            registered
        );

        return FurnitureRecords.IsDefaultLogic(logic) ? null : logic;
    }

    /// <summary>
    /// Whether a definition runs the default (or a logic the server does not have) where the
    /// server has one for it. A registered logic staff chose over the matching one is their call.
    /// </summary>
    public static bool IsMismatch(
        string logic,
        string? matching,
        IReadOnlySet<string> registered
    ) =>
        matching is not null
        && logic != matching
        && (FurnitureRecords.IsDefaultLogic(logic) || !registered.Contains(logic));

    /// <summary>The client logic each asset's newest read file names, by asset.</summary>
    private static Dictionary<string, string> ClientLogics(
        IReadOnlyDictionary<(string Asset, int Revision), HabboFurnitureAssetEntity> files
    ) =>
        files
            .Values.Where(x => x.Error is null && !string.IsNullOrEmpty(x.LogicType))
            .GroupBy(x => x.AssetName, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.MaxBy(x => x.Revision)!.LogicType!,
                StringComparer.Ordinal
            );

    /// <summary>
    /// The revision Habbo's newest release names for each asset: the highest of the classnames
    /// drawn from it, as a sync takes it (<see cref="HabboAssetLists.Furniture"/>).
    /// </summary>
    private static Dictionary<string, string> AssetRevisions(HabboReleaseItems.Loaded? latest)
    {
        var revisions = new Dictionary<string, int>(StringComparer.Ordinal);

        if (latest is null)
            return [];

        foreach (var (key, item) in latest.Items)
        {
            var asset = FurnitureAssetNames.BundleNameOf(key.ClassName);

            revisions[asset] = Math.Max(
                revisions.GetValueOrDefault(asset),
                HabboFurnitureFiles.Revision(item)
            );
        }

        return revisions.ToDictionary(
            x => x.Key,
            x => x.Value.ToString(CultureInfo.InvariantCulture),
            StringComparer.Ordinal
        );
    }

    /// <summary>What the tags are made from, as cheaply as the database can say it.</summary>
    private static async Task<Key> KeyAsync(
        TurboDbContext dbCtx,
        FurnitureOfferCatalog.Stamps stamps,
        HabboReleaseItems.Loaded? latest,
        int logicsVersion,
        CancellationToken ct
    )
    {
        var definitions = dbCtx.FurnitureDefinitions.AsNoTracking();
        var bundles = dbCtx
            .AssetBundles.AsNoTracking()
            .Where(x => x.Kind == AssetBundleKind.Furniture);

        return new Key(
            await definitions.CountAsync(ct).ConfigureAwait(false),
            await definitions.MaxAsync(x => (int?)x.Id, ct).ConfigureAwait(false),
            await dbCtx
                .GamedataChangeSets.AsNoTracking()
                .MaxAsync(x => (int?)x.Id, ct)
                .ConfigureAwait(false),
            await bundles.CountAsync(ct).ConfigureAwait(false),
            await bundles.MaxAsync(x => (DateTime?)x.UpdatedAt, ct).ConfigureAwait(false),
            await dbCtx.HabboFurnitureAssets.AsNoTracking().CountAsync(ct).ConfigureAwait(false),
            latest?.Release.Id,
            stamps,
            logicsVersion
        );
    }

    private sealed record Bundle(
        string Name,
        bool HasFile,
        string? Error,
        AssetBundleSource Source,
        string? Revision,
        long Size
    );

    private sealed record Key(
        int Definitions,
        int? MaxDefinitionId,
        int? ChangeSet,
        int Bundles,
        DateTime? BundleUpdated,
        int AssetFiles,
        int? Release,
        FurnitureOfferCatalog.Stamps Stamps,
        int Logics
    );

    private sealed record Built(Key Key, List<FurnitureListItem> Items);
}
