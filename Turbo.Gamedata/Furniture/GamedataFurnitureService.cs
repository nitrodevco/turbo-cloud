using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Gamedata;
using Turbo.Database.Extensions;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Habbo;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// Habbo's furniture taken in, and definitions edited (<see cref="IGamedataFurnitureService"/>).
/// Definitions are matched to Habbo's items by kind and classname. Habbo's items the hotel no
/// longer has a match for are added; the hotel's definitions Habbo dropped are left alone. Every
/// write reloads the definition provider and has the files built again.
/// </summary>
internal sealed class GamedataFurnitureService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config,
    IFurnitureDefinitionProvider definitions,
    IGamedataFileService files,
    FurnitureOfferCatalog offers,
    HabboReleaseItems habboItems,
    HabboFurnitureFiles furnitureFiles,
    GamedataWriteLock writes,
    TimeProvider time,
    ILogger<GamedataFurnitureService> logger
) : IGamedataFurnitureService
{
    private readonly GamedataConfig _config = config.Value;

    public async Task<FurnitureImportPreview?> PreviewImportAsync(
        int? releaseId,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var release = await FindReleaseAsync(dbCtx, releaseId, ct).ConfigureAwait(false);

        if (release is null)
            return null;

        var files = await furnitureFiles.LoadAsync(ct).ConfigureAwait(false);
        var plan = await PlanAsync(dbCtx, release, files, tracked: false, ct).ConfigureAwait(false);
        var touched = plan.Items.Where(x => x.Action is not null).ToList();

        return new FurnitureImportPreview
        {
            Release = release.ToSnapshot(),
            Added = plan.Count(FurnitureImportAction.Add),
            Updated = plan.Count(FurnitureImportAction.Update),
            Kept = plan.Count(FurnitureImportAction.Keep),
            Unchanged = plan.Items.Count(x => x.Action is null),
            FilesToRead = HabboFurnitureFiles.Missing(plan.Items.Select(x => x.Habbo), files).Count,
            Items = [.. touched.Take(_config.PreviewItemLimit).Select(x => x.ToSnapshot())],
            Truncated = touched.Count > _config.PreviewItemLimit,
        };
    }

    public async Task<GamedataChangeSetSnapshot?> ImportAsync(
        int releaseId,
        PlayerId? player,
        CancellationToken ct
    )
    {
        return await writes
            .RunAsync(() => ImportLockedAsync(releaseId, player, ct), ct)
            .ConfigureAwait(false);
    }

    private async Task<GamedataChangeSetSnapshot?> ImportLockedAsync(
        int releaseId,
        PlayerId? player,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var release = await FindReleaseAsync(dbCtx, releaseId, ct).ConfigureAwait(false);

        if (release is null)
            return null;

        var files = await furnitureFiles.LoadAsync(ct).ConfigureAwait(false);
        var plan = await PlanAsync(dbCtx, release, files, tracked: true, ct).ConfigureAwait(false);
        var now = time.GetUtcNow().UtcDateTime;
        var changes = new List<(GamedataChangeEntity Change, object Row)>();
        var added = 0;
        var updated = 0;

        var tx = await dbCtx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        await using var txScope = tx.ConfigureAwait(false);

        foreach (var item in plan.Items)
        {
            if (item.Action == FurnitureImportAction.Add)
            {
                var definition = FurnitureRecords.Create(
                    item.ProductType,
                    plan.AllocateSpriteId(item.ProductType, item.SpriteId),
                    item.ClassName,
                    item.Habbo
                );

                dbCtx.FurnitureDefinitions.Add(definition);
                changes.Add(
                    (Change(item.ClassName, GamedataRecordType.FurnitureDefinition), definition)
                );
                added++;
            }
            else if (item.Action == FurnitureImportAction.Update)
            {
                var definition = item.Definition!;
                var keys = item.Fields.Where(x => !x.Kept).Select(x => x.Field).ToList();
                var before = FurnitureRecords.Fields(definition, keys);

                foreach (var key in keys)
                    FurnitureFields.ByKey[key].Write(definition, item.Habbo[key]);

                var change = Change(item.ClassName, GamedataRecordType.FurnitureDefinition);

                change.RecordId = definition.Id;
                change.Before = before.ToJsonString();
                change.After = FurnitureRecords.Fields(definition, keys).ToJsonString();
                changes.Add((change, definition));
                updated++;
            }

            var data = item.Habbo.ToJsonString();

            if (item.Base is { } known)
            {
                if (known.Data == data && known.SpriteId == item.SpriteId)
                    continue;

                var change = Change(item.ClassName, GamedataRecordType.HabboFurniture);

                change.RecordId = known.Id;
                change.Before = known.Data;
                change.After = data;
                changes.Add((change, known));

                known.Data = data;
                known.SpriteId = item.SpriteId;
                known.HabboReleaseEntityId = release.Id;
            }
            else
            {
                var row = new HabboFurnitureEntity
                {
                    ProductType = item.ProductType,
                    ClassName = item.ClassName,
                    SpriteId = item.SpriteId,
                    Data = data,
                    HabboReleaseEntityId = release.Id,
                };

                var change = Change(item.ClassName, GamedataRecordType.HabboFurniture);

                change.After = data;
                dbCtx.HabboFurniture.Add(row);
                changes.Add((change, row));
            }
        }

        release.ImportedAt = now;

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        GamedataChangeSetEntity? changeSet = null;

        if (changes.Count > 0)
        {
            changeSet = new GamedataChangeSetEntity
            {
                Kind = GamedataChangeKind.Import,
                Summary = Summary(
                    $"Habbo {release.Revision}: {added} added, {updated} updated, {plan.Count(FurnitureImportAction.Keep)} kept as the hotel has them"
                ),
                PlayerEntityId = player?.Value,
                HabboReleaseEntityId = release.Id,
                Changes = [],
            };

            foreach (var (change, row) in changes)
            {
                // Rows made by this import have their ids only now that they are saved.
                change.RecordId = row switch
                {
                    FurnitureDefinitionEntity definition => definition.Id,
                    HabboFurnitureEntity habbo => habbo.Id,
                    _ => change.RecordId,
                };

                if (change.Before is null && row is FurnitureDefinitionEntity made)
                    change.After = FurnitureRecords.Created(made).ToJsonString();

                changeSet.Changes.Add(change);
            }

            dbCtx.GamedataChangeSets.Add(changeSet);

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Imported Habbo release {ReleaseId} ({Revision}): {Added} furniture added, {Updated} updated, {Kept} kept as the hotel has them",
            release.Id,
            release.Revision,
            added,
            updated,
            plan.Count(FurnitureImportAction.Keep)
        );

        await AfterWriteAsync(ct).ConfigureAwait(false);

        return changeSet?.ToSnapshot(changeSet.Changes!.Count);
    }

    public async Task<FurnitureDefinitionDetail?> GetDefinitionAsync(int id, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var definition = await dbCtx
            .FurnitureDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        return definition is null
            ? null
            : await DetailAsync(dbCtx, definition, ct).ConfigureAwait(false);
    }

    public async Task<FurnitureDefinitionDetail?> UpdateDefinitionAsync(
        int id,
        JsonObject fields,
        PlayerId player,
        CancellationToken ct
    )
    {
        return await writes
            .RunAsync(() => UpdateDefinitionLockedAsync(id, fields, player, ct), ct)
            .ConfigureAwait(false);
    }

    private async Task<FurnitureDefinitionDetail?> UpdateDefinitionLockedAsync(
        int id,
        JsonObject fields,
        PlayerId player,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var definition = await dbCtx
            .FurnitureDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (definition is null)
            return null;

        var isWall = definition.ProductType == ProductType.Wall;
        var incoming = new Dictionary<string, JsonNode?>();

        // Every value is checked before any is written, so a bad one changes nothing.
        foreach (var (key, value) in fields)
        {
            if (!FurnitureFields.ByKey.TryGetValue(key, out var field) || !field.AppliesTo(isWall))
                throw new ArgumentException(
                    $"{key} is not a field a {(isWall ? "wall" : "floor")} item's definition holds.",
                    key
                );

            var normalized = field.Normalize(value);

            if (
                field.MaxLength is { } max
                && normalized?.GetValue<string>() is { Length: var length }
                && length > max
            )
                throw new ArgumentException($"{key} can be at most {max} characters.", key);

            if (!FurnitureField.Same(normalized, field.Read(definition)))
                incoming[key] = normalized;
        }

        if (incoming.Count > 0)
        {
            var before = FurnitureRecords.Fields(definition, incoming.Keys);

            foreach (var (key, value) in incoming)
                FurnitureFields.ByKey[key].Write(definition, value);

            dbCtx.GamedataChangeSets.Add(
                new GamedataChangeSetEntity
                {
                    Kind = GamedataChangeKind.Edit,
                    Summary = Summary(
                        $"Edited {definition.Name}: {string.Join(", ", incoming.Keys)}"
                    ),
                    PlayerEntityId = player.Value,
                    Changes =
                    [
                        new GamedataChangeEntity
                        {
                            RecordType = GamedataRecordType.FurnitureDefinition,
                            RecordId = definition.Id,
                            Label = Label(definition.Name),
                            Before = before.ToJsonString(),
                            After = FurnitureRecords
                                .Fields(definition, incoming.Keys)
                                .ToJsonString(),
                        },
                    ],
                }
            );

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            logger.LogInformation(
                "Player {PlayerId} edited furniture definition {DefinitionId} ({ClassName}): {Fields}",
                player.Value,
                definition.Id,
                definition.Name,
                string.Join(", ", incoming.Keys)
            );

            await AfterWriteAsync(ct).ConfigureAwait(false);
        }

        return await DetailAsync(dbCtx, definition, ct).ConfigureAwait(false);
    }

    public async Task<HabboValuesPreview> PreviewHabboValuesAsync(
        IReadOnlyCollection<string> fields,
        CancellationToken ct
    )
    {
        var chosen = Resolve(fields);
        var habbo = await habboItems.GetLatestAsync(ct).ConfigureAwait(false);
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var files = await furnitureFiles.LoadAsync(ct).ConfigureAwait(false);
        var differences = await HabboDifferencesAsync(
                dbCtx,
                chosen,
                habbo,
                files,
                tracked: false,
                ct
            )
            .ConfigureAwait(false);

        return new HabboValuesPreview
        {
            ByField = chosen.ToImmutableDictionary(
                x => x.Key,
                x => differences.Count(d => d.Keys.Contains(x.Key))
            ),
            Definitions = differences.Count,
            Release = habbo?.Release,
        };
    }

    public Task<GamedataChangeSetSnapshot?> TakeHabboValuesAsync(
        IReadOnlyCollection<string> fields,
        PlayerId player,
        CancellationToken ct
    ) => writes.RunAsync(() => TakeHabboValuesLockedAsync(fields, player, ct), ct);

    private async Task<GamedataChangeSetSnapshot?> TakeHabboValuesLockedAsync(
        IReadOnlyCollection<string> fields,
        PlayerId player,
        CancellationToken ct
    )
    {
        var chosen = Resolve(fields);
        var habbo = await habboItems.GetLatestAsync(ct).ConfigureAwait(false);
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var files = await furnitureFiles.LoadAsync(ct).ConfigureAwait(false);
        var differences = await HabboDifferencesAsync(
                dbCtx,
                chosen,
                habbo,
                files,
                tracked: true,
                ct
            )
            .ConfigureAwait(false);

        if (differences.Count == 0)
            return null;

        var changeSet = new GamedataChangeSetEntity
        {
            Kind = GamedataChangeKind.HabboValues,
            Summary = Summary(
                $"Habbo's {string.Join(", ", chosen.Select(x => x.Key))} ({habbo!.Release.Revision}) for {differences.Count} furniture"
            ),
            PlayerEntityId = player.Value,
            Changes = [],
        };

        foreach (var (definition, keys, item) in differences)
        {
            var before = FurnitureRecords.Fields(definition, keys);

            foreach (var key in keys)
                FurnitureFields.ByKey[key].Write(definition, item[key]);

            changeSet.Changes.Add(
                new GamedataChangeEntity
                {
                    RecordType = GamedataRecordType.FurnitureDefinition,
                    RecordId = definition.Id,
                    Label = Label(definition.Name),
                    Before = before.ToJsonString(),
                    After = FurnitureRecords.Fields(definition, keys).ToJsonString(),
                }
            );
        }

        dbCtx.GamedataChangeSets.Add(changeSet);

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Player {PlayerId} put Habbo's {Fields} back on {Count} furniture definitions",
            player.Value,
            string.Join(", ", chosen.Select(x => x.Key)),
            differences.Count
        );

        await AfterWriteAsync(ct).ConfigureAwait(false);

        return changeSet.ToSnapshot(changeSet.Changes.Count);
    }

    /// <summary>The fields by their furnidata keys, each once; a key no definition holds is refused.</summary>
    private static List<FurnitureField> Resolve(IReadOnlyCollection<string> keys)
    {
        var fields = new List<FurnitureField>();

        foreach (var key in keys.Select(x => x.Trim()).Distinct())
            fields.Add(
                FurnitureFields.ByKey.TryGetValue(key, out var field)
                    ? field
                    : throw new ArgumentException(
                        $"{key} is not a field a furniture definition holds.",
                        nameof(keys)
                    )
            );

        if (fields.Count == 0)
            throw new ArgumentException("Choose at least one field.", nameof(keys));

        return fields;
    }

    /// <summary>
    /// Each definition of furniture Habbo's newest release has (matched as an import matches it),
    /// with the chosen fields where its value is not Habbo's.
    /// </summary>
    private static async Task<
        List<(FurnitureDefinitionEntity Definition, List<string> Keys, JsonObject Habbo)>
    > HabboDifferencesAsync(
        TurboDbContext dbCtx,
        List<FurnitureField> fields,
        HabboReleaseItems.Loaded? habbo,
        IReadOnlyDictionary<(string Asset, int Revision), HabboFurnitureAssetEntity> files,
        bool tracked,
        CancellationToken ct
    )
    {
        var differences = new List<(FurnitureDefinitionEntity, List<string>, JsonObject)>();

        if (habbo is null)
            return differences;

        var definitionQuery = dbCtx.FurnitureDefinitions.Where(x =>
            x.ProductType == ProductType.Floor || x.ProductType == ProductType.Wall
        );

        if (!tracked)
            definitionQuery = definitionQuery.AsNoTracking();

        var definitions = (await definitionQuery.ToListAsync(ct).ConfigureAwait(false))
            .GroupBy(x => (x.ProductType, x.Name))
            .Select(g => g.MinBy(x => x.Id)!);

        foreach (var definition in definitions)
        {
            if (!habbo.Items.TryGetValue((definition.ProductType, definition.Name), out var raw))
                continue;

            var item = HabboFurnitureFiles.Enrich(raw, files);
            var isWall = definition.ProductType == ProductType.Wall;
            var keys = fields
                .Where(field =>
                    field.AppliesTo(isWall)
                    && field.IsIn(item)
                    && !FurnitureField.Same(
                        field.Read(definition),
                        field.Normalize(item[field.Key])
                    )
                )
                .Select(field => field.Key)
                .ToList();

            if (keys.Count > 0)
                differences.Add((definition, keys, item));
        }

        return differences;
    }

    private async Task<FurnitureDefinitionDetail> DetailAsync(
        TurboDbContext dbCtx,
        FurnitureDefinitionEntity definition,
        CancellationToken ct
    )
    {
        var stamps = await offers.GetAsync(ct).ConfigureAwait(false);
        var latest = await habboItems.GetLatestAsync(ct).ConfigureAwait(false);
        JsonObject? habbo = null;

        latest?.Items.TryGetValue((definition.ProductType, definition.Name), out habbo);

        var file = habbo is null
            ? null
            : await dbCtx
                .HabboFurnitureAssets.AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.AssetName == HabboFurnitureFiles.AssetName(definition.Name)
                        && x.Revision == HabboFurnitureFiles.Revision(habbo),
                    ct
                )
                .ConfigureAwait(false);

        if (habbo is not null && file is { Error: null })
            habbo = HabboFurnitureFiles.Enrich(
                habbo,
                new Dictionary<(string, int), HabboFurnitureAssetEntity>
                {
                    [(file.AssetName, file.Revision)] = file,
                }
            );

        return new FurnitureDefinitionDetail
        {
            Id = definition.Id,
            ProductType = definition.ProductType,
            ClassName = definition.Name,
            Item = FurnitureDataWriter
                .Item(definition, stamps.Offers, stamps.BuildersClubOffers)
                .ToJsonString(),
            Habbo = habbo?.ToJsonString(),
            HabboFile = file?.Error ?? file?.Info,
            HabboFileRead = file is { Error: null },
            States = definition.TotalStates,
        };
    }

    /// <summary>Rooms and the files see the definitions as they now are.</summary>
    private async Task AfterWriteAsync(CancellationToken ct)
    {
        files.Invalidate(GamedataFiles.FURNITURE_DATA);

        try
        {
            await definitions.ReloadAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // The rows are written; rooms pick them up on the next reload.
            logger.LogError(ex, "Reloading furniture definitions after a gamedata change failed");
        }
    }

    private Task<HabboReleaseEntity?> FindReleaseAsync(
        TurboDbContext dbCtx,
        int? releaseId,
        CancellationToken ct
    ) =>
        releaseId is { } id
            ? dbCtx.HabboReleases.FirstOrDefaultAsync(x => x.Id == id, ct)
            : dbCtx
                .HabboReleases.Where(x => x.Domain == _config.HabboDomain)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct);

    /// <summary>Every item of the release, matched to the hotel's definition of it and to Habbo's last version of it.</summary>
    private static async Task<ImportPlan> PlanAsync(
        TurboDbContext dbCtx,
        HabboReleaseEntity release,
        IReadOnlyDictionary<(string Asset, int Revision), HabboFurnitureAssetEntity> files,
        bool tracked,
        CancellationToken ct
    )
    {
        var definitionQuery = dbCtx.FurnitureDefinitions.Where(x =>
            x.ProductType == ProductType.Floor || x.ProductType == ProductType.Wall
        );
        var habboQuery = dbCtx.HabboFurniture.AsQueryable();

        if (!tracked)
        {
            definitionQuery = definitionQuery.AsNoTracking();
            habboQuery = habboQuery.AsNoTracking();
        }

        var allDefinitions = await definitionQuery.ToListAsync(ct).ConfigureAwait(false);
        var bases = (await habboQuery.ToListAsync(ct).ConfigureAwait(false)).ToDictionary(x =>
            (x.ProductType, x.ClassName)
        );

        // Should the hotel have two definitions of one classname, the oldest is Habbo's.
        var byName = allDefinitions
            .GroupBy(x => (x.ProductType, x.Name))
            .ToDictionary(g => g.Key, g => g.MinBy(x => x.Id)!);
        var plan = new ImportPlan(allDefinitions);
        var data = JsonNode.Parse(GamedataBytes.Decompress(release.FurnitureData));

        foreach (var (list, type) in ImportPlan.LISTS)
        {
            if (data?[list]?["furnitype"] is not JsonArray items)
                continue;

            foreach (var node in items)
            {
                if (
                    node is not JsonObject item
                    || item["classname"]?.GetValue<string>() is not { Length: > 0 } className
                )
                    continue;

                // With what its asset file said (its states), when the file was read.
                var habbo = HabboFurnitureFiles.Enrich(item, files);
                var spriteId = FurnitureValues.ToInt(habbo["id"], "id");

                plan.HabboSpriteIds(type).Add(spriteId);

                byName.TryGetValue((type, className), out var definition);
                bases.TryGetValue((type, className), out var known);

                var fields = definition is null
                    ? []
                    : FurnitureMerge.Compare(
                        definition,
                        habbo,
                        known is null ? null : JsonNode.Parse(known.Data) as JsonObject
                    );
                FurnitureImportAction? action =
                    definition is null ? FurnitureImportAction.Add
                    : fields.Any(x => !x.Kept) ? FurnitureImportAction.Update
                    : fields.Length > 0 ? FurnitureImportAction.Keep
                    : null;

                plan.Items.Add(
                    new PlannedItem(
                        type,
                        className,
                        spriteId,
                        habbo,
                        definition,
                        known,
                        fields,
                        action
                    )
                );
            }
        }

        return plan;
    }

    private static GamedataChangeEntity Change(string className, GamedataRecordType type) =>
        new()
        {
            RecordType = type,
            RecordId = 0,
            Label = Label(className),
        };

    private static string Label(string className) =>
        className.Length <= GamedataChangeEntity.LABEL_MAX_LENGTH
            ? className
            : className[..GamedataChangeEntity.LABEL_MAX_LENGTH];

    private static string Summary(string summary) =>
        summary.Length <= GamedataChangeSetEntity.SUMMARY_MAX_LENGTH
            ? summary
            : summary[..GamedataChangeSetEntity.SUMMARY_MAX_LENGTH];

    private sealed record PlannedItem(
        ProductType ProductType,
        string ClassName,
        int SpriteId,
        JsonObject Habbo,
        FurnitureDefinitionEntity? Definition,
        HabboFurnitureEntity? Base,
        ImmutableArray<FurnitureFieldChange> Fields,
        FurnitureImportAction? Action
    )
    {
        public FurnitureImportItem ToSnapshot() =>
            new()
            {
                ProductType = ProductType,
                ClassName = ClassName,
                SpriteId = SpriteId,
                DefinitionId = Definition?.Id,
                Action = Action!.Value,
                Fields = Fields,
            };
    }

    private sealed class ImportPlan(IReadOnlyCollection<FurnitureDefinitionEntity> definitions)
    {
        public static readonly (string List, ProductType Type)[] LISTS =
        [
            ("roomitemtypes", ProductType.Floor),
            ("wallitemtypes", ProductType.Wall),
        ];

        private readonly Dictionary<ProductType, HashSet<int>> _habboIds = [];
        private readonly Dictionary<ProductType, HashSet<int>> _taken = [];

        public List<PlannedItem> Items { get; } = [];

        public int Count(FurnitureImportAction action) => Items.Count(x => x.Action == action);

        public HashSet<int> HabboSpriteIds(ProductType type) =>
            _habboIds.TryGetValue(type, out var ids) ? ids : _habboIds[type] = [];

        /// <summary>
        /// Habbo's sprite id for a new item, unless a definition of the hotel's own holds it; then
        /// one above every id the hotel and Habbo use, so no later Habbo item finds its id taken.
        /// </summary>
        public int AllocateSpriteId(ProductType type, int habboId)
        {
            if (!_taken.TryGetValue(type, out var taken))
                _taken[type] = taken = [
                    .. definitions.Where(x => x.ProductType == type).Select(x => x.SpriteId),
                ];

            var id = habboId;

            if (taken.Contains(id))
                id = Math.Max(taken.Max(), HabboSpriteIds(type).DefaultIfEmpty(0).Max()) + 1;

            taken.Add(id);

            return id;
        }
    }
}
