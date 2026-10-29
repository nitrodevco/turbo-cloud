using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Assets;
using Turbo.Assets.Furniture;
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
using Turbo.Primitives.Rooms.Providers;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// Habbo's furniture taken in, and definitions edited (<see cref="IGamedataFurnitureService"/>).
/// Definitions are matched to Habbo's items by kind and classname. Habbo's items the hotel no
/// longer has a match for are added; the hotel's definitions Habbo dropped are left alone. Every
/// write reloads the definition provider and has the files built again. A definition made for a
/// new item gets the logic the server registers under its classname, if there is one.
/// </summary>
internal sealed class GamedataFurnitureService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config,
    IFurnitureDefinitionProvider definitions,
    IGamedataFileService files,
    FurnitureOfferCatalog offers,
    HabboReleaseItems habboItems,
    HabboFurnitureFiles furnitureFiles,
    FurnitureStatusIndex statuses,
    GamedataWriteLock writes,
    IRoomObjectLogicProvider logics,
    IAssetBundleStore bundles,
    TimeProvider time,
    ILogger<GamedataFurnitureService> logger
) : IGamedataFurnitureService
{
    // specialtype, which with the sprite id tells the client's furniture apart.
    private const string SPECIAL_TYPE_KEY = "specialtype";

    // Colour variants are named name*N (chair*2).
    private const string COLOUR_VARIANT_MARK = "*";

    private static readonly JsonSerializerOptions DRAFT_JSON = new(JsonSerializerDefaults.Web);

    private readonly GamedataConfig _config = config.Value;
    private readonly GamedataPreviewCache<FurnitureDataVersion, FurnitureImportPreview> _previews =
        new();

    public async Task<FurnitureImportPreview?> PreviewImportAsync(
        int? releaseId,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var version = await FurnitureDataVersion
            .ReadAsync(dbCtx, _config.HabboDomain, ct)
            .ConfigureAwait(false);

        return await _previews
            .GetAsync(version, releaseId, () => WorkOutPreviewAsync(dbCtx, releaseId, ct), ct)
            .ConfigureAwait(false);
    }

    private async Task<FurnitureImportPreview?> WorkOutPreviewAsync(
        TurboDbContext dbCtx,
        int? releaseId,
        CancellationToken ct
    )
    {
        var release = await dbCtx
            .HabboReleases.FindOrLatestAsync(releaseId, _config.HabboDomain, ct)
            .ConfigureAwait(false);

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

        var release = await dbCtx
            .HabboReleases.FindOrLatestAsync(releaseId, _config.HabboDomain, ct)
            .ConfigureAwait(false);

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
                    item.Logic!,
                    item.Habbo
                );

                // What Sulake's data says the class does: a fridge's drinks, a crackable's
                // contents. Habbo's fields were written first, so a state count or usage the
                // behaviour knows better replaces them, as the migration's SQL did.
                FurnitureBehaviourApplier.Apply(definition);

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
            changeSet = GamedataChangeSets.Create(
                GamedataChangeKind.Import,
                $"Habbo {release.Revision}: {added} added, {updated} updated, {plan.Count(FurnitureImportAction.Keep)} kept as the hotel has them",
                player,
                []
            );
            changeSet.HabboReleaseEntityId = release.Id;

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

                changeSet.Changes!.Add(change);
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

    public async Task<FurnitureListPage> ListAsync(
        string? query,
        ProductType? type,
        FurnitureTag? tag,
        int page,
        CancellationToken ct
    )
    {
        var pageSize = Math.Max(1, _config.FurniturePageSize);
        var all = await statuses.GetAsync(ct).ConfigureAwait(false);
        var text = query?.Trim() ?? string.Empty;
        var number = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : (int?)null;
        var searched = all.Where(x =>
                (type is null || x.ProductType == type)
                && (
                    text.Length == 0
                    || x.Id == number
                    || x.SpriteId == number
                    || x.ClassName.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || x.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true
                )
            )
            .ToList();
        var found = tag is { } only
            ? searched.Where(x => x.Tags.Contains(only)).ToList()
            : searched;

        return new FurnitureListPage
        {
            Items = [.. found.Skip(Math.Max(0, page) * pageSize).Take(pageSize)],
            Total = found.Count,
            PageSize = pageSize,
            TagCounts = searched
                .SelectMany(x => x.Tags)
                .GroupBy(x => x)
                .ToImmutableDictionary(g => g.Key, g => g.Count()),
            Searched = searched.Count,
        };
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

        var incoming = new Dictionary<string, JsonNode?>();

        // Every value is checked before any is written, so a bad one changes nothing.
        foreach (var (key, normalized) in Checked(fields, definition.ProductType))
            if (!FurnitureField.Same(normalized, FurnitureFields.ByKey[key].Read(definition)))
                incoming[key] = normalized;

        await CheckIdentityAsync(dbCtx, definition, incoming, ct).ConfigureAwait(false);

        if (incoming.Count > 0)
        {
            var before = FurnitureRecords.Fields(definition, incoming.Keys);

            foreach (var (key, value) in incoming)
                FurnitureFields.ByKey[key].Write(definition, value);

            dbCtx.GamedataChangeSets.Add(
                GamedataChangeSets.Create(
                    GamedataChangeKind.Edit,
                    $"Edited {definition.Name}: {string.Join(", ", incoming.Keys)}",
                    player,
                    [
                        GamedataChangeSets.Change(
                            GamedataRecordType.FurnitureDefinition,
                            definition.Name,
                            before.ToJsonString(),
                            FurnitureRecords.Fields(definition, incoming.Keys).ToJsonString(),
                            definition.Id
                        ),
                    ]
                )
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

    /// <summary>
    /// The fields' values as a definition of the kind holds them. Throws
    /// <see cref="ArgumentException"/> for a key it can't hold or a value of the wrong kind.
    /// </summary>
    private static Dictionary<string, JsonNode?> Checked(JsonObject fields, ProductType type)
    {
        var isWall = type == ProductType.Wall;
        var values = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);

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

            if (field.Check(normalized) is { } problem)
                throw new ArgumentException(problem, key);

            values[key] = normalized;
        }

        return values;
    }

    public async Task<FurnitureDraft?> DraftFromBundleAsync(string bundle, CancellationToken ct)
    {
        if (!bundles.IsValidName(bundle))
            return null;

        var path = bundles.FullPathOf(AssetBundleKind.Furniture, bundle);

        if (!File.Exists(path))
            return null;

        FurnitureAssetInfo info;

        try
        {
            info = FurnitureAssetReader.Read(
                await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false),
                bundle
            );
        }
        catch (AssetFormatException ex)
        {
            throw new ArgumentException($"The bundle {bundle} can't be read: {ex.Message}", ex);
        }

        var type = FurnitureDrafts.TypeOf(info);
        var logic = FurnitureDrafts.LogicOf(
            type,
            bundle,
            info,
            logics.LogicTypes.ToHashSet(StringComparer.Ordinal)
        );
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var variants = bundle + COLOUR_VARIANT_MARK;
        var existing = await dbCtx
            .FurnitureDefinitions.AsNoTracking()
            .Where(x => x.Name == bundle || x.Name.StartsWith(variants))
            .OrderBy(x => x.Id)
            .Select(x => new FurnitureDraftDefinition
            {
                Id = x.Id,
                ProductType = x.ProductType,
                ClassName = x.Name,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new FurnitureDraft
        {
            Bundle = bundle,
            ProductType = type,
            Fields = FurnitureDrafts.Fields(bundle, type, logic, info).ToJsonString(),
            File = JsonSerializer.Serialize(info, DRAFT_JSON),
            Colors = FurnitureDrafts.Colors(info),
            Existing = [.. existing],
        };
    }

    public Task<ImmutableArray<int>> CreateDefinitionsAsync(
        ProductType type,
        JsonObject fields,
        IReadOnlyCollection<int> colors,
        PlayerId player,
        CancellationToken ct
    ) => writes.RunAsync(() => CreateDefinitionsLockedAsync(type, fields, colors, player, ct), ct);

    private async Task<ImmutableArray<int>> CreateDefinitionsLockedAsync(
        ProductType type,
        JsonObject fields,
        IReadOnlyCollection<int> colors,
        PlayerId player,
        CancellationToken ct
    )
    {
        if (type is not (ProductType.Floor or ProductType.Wall))
            throw new ArgumentException("A definition is of a floor or a wall item.", nameof(type));

        var values = Checked(fields, type);

        if (
            !values.Remove(FurnitureRecords.CLASS_NAME_KEY, out var name)
            || name?.GetValue<string>() is not { Length: > 0 } className
        )
            throw new ArgumentException(
                "A definition needs a classname.",
                FurnitureRecords.CLASS_NAME_KEY
            );

        if (className.Contains(COLOUR_VARIANT_MARK, StringComparison.Ordinal))
            throw new ArgumentException(
                $"The classname is the plain item's: its colours are made as {className}{COLOUR_VARIANT_MARK}N.",
                FurnitureRecords.CLASS_NAME_KEY
            );

        if (colors.Any(x => x <= 0))
            throw new ArgumentException("A colour is a number above 0.", nameof(colors));

        values.Remove(FurnitureRecords.SPRITE_ID_KEY, out var givenId);
        values.Remove(FurnitureRecords.LOGIC_KEY, out var givenLogic);

        var registered = logics.LogicTypes.ToHashSet(StringComparer.Ordinal);
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        // Above every sprite id the hotel's definitions and Habbo's items of the kind use, so a
        // later Habbo item finds its own free.
        var highest = Math.Max(
            await dbCtx
                .FurnitureDefinitions.Where(x => x.ProductType == type)
                .MaxAsync(x => (int?)x.SpriteId, ct)
                .ConfigureAwait(false)
                ?? 0,
            await dbCtx
                .HabboFurniture.Where(x => x.ProductType == type)
                .MaxAsync(x => (int?)x.SpriteId, ct)
                .ConfigureAwait(false)
                ?? 0
        );
        var made = new List<FurnitureDefinitionEntity>();

        foreach (var color in colors.Distinct().Order().Prepend(0))
        {
            var variant = color == 0 ? className : $"{className}{COLOUR_VARIANT_MARK}{color}";
            var spriteId = color == 0 && givenId is not null ? givenId.GetValue<int>() : ++highest;
            var definition = FurnitureRecords.Blank(
                type,
                spriteId,
                variant,
                givenLogic?.GetValue<string>()
                    ?? FurnitureRecords.LogicFor(type, variant, registered)
            );

            foreach (var (key, value) in values)
                FurnitureFields.ByKey[key].Write(definition, value);

            await CheckIdentityAsync(
                    dbCtx,
                    definition,
                    new Dictionary<string, JsonNode?>
                    {
                        [FurnitureRecords.CLASS_NAME_KEY] = variant,
                        [FurnitureRecords.SPRITE_ID_KEY] = spriteId,
                    },
                    ct
                )
                .ConfigureAwait(false);

            highest = Math.Max(highest, spriteId);
            made.Add(definition);
        }

        dbCtx.FurnitureDefinitions.AddRange(made);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        dbCtx.GamedataChangeSets.Add(
            GamedataChangeSets.Create(
                GamedataChangeKind.Edit,
                made.Count == 1
                    ? $"Made {className}"
                    : $"Made {className} and {made.Count - 1} colours of it",
                player,
                [
                    .. made.Select(x =>
                        GamedataChangeSets.Change(
                            GamedataRecordType.FurnitureDefinition,
                            x.Name,
                            null,
                            FurnitureRecords.Created(x).ToJsonString(),
                            x.Id
                        )
                    ),
                ]
            )
        );

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Player {PlayerId} made furniture definitions {ClassNames}",
            player.Value,
            string.Join(", ", made.Select(x => $"{x.Name} ({x.Id})"))
        );

        await AfterWriteAsync(ct).ConfigureAwait(false);

        return [.. made.Select(x => x.Id)];
    }

    /// <summary>
    /// Refuses a classname another definition of the kind has (an import would not know which is
    /// Habbo's), and a sprite id and special type another of the kind has (the client tells
    /// furniture apart by them; the table keeps them unique).
    /// </summary>
    private static async Task CheckIdentityAsync(
        TurboDbContext dbCtx,
        FurnitureDefinitionEntity definition,
        IReadOnlyDictionary<string, JsonNode?> incoming,
        CancellationToken ct
    )
    {
        var others = dbCtx
            .FurnitureDefinitions.AsNoTracking()
            .Where(x => x.Id != definition.Id && x.ProductType == definition.ProductType);

        if (incoming.TryGetValue(FurnitureRecords.CLASS_NAME_KEY, out var name))
        {
            var className = name!.GetValue<string>();

            if (await others.AnyAsync(x => x.Name == className, ct).ConfigureAwait(false))
                throw new ArgumentException(
                    $"Another {Kind(definition)} definition is called {className}.",
                    FurnitureRecords.CLASS_NAME_KEY
                );
        }

        if (
            incoming.ContainsKey(FurnitureRecords.SPRITE_ID_KEY)
            || incoming.ContainsKey(SPECIAL_TYPE_KEY)
        )
        {
            var spriteId = incoming.TryGetValue(FurnitureRecords.SPRITE_ID_KEY, out var id)
                ? id!.GetValue<int>()
                : definition.SpriteId;
            var category = incoming.TryGetValue(SPECIAL_TYPE_KEY, out var special)
                ? (FurnitureCategory)special!.GetValue<int>()
                : definition.FurniCategory;
            var taken = await others
                .Where(x => x.SpriteId == spriteId && x.FurniCategory == category)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (taken is not null)
                throw new ArgumentException(
                    $"{taken} has sprite id {spriteId} with that special type already.",
                    FurnitureRecords.SPRITE_ID_KEY
                );
        }
    }

    private static string Kind(FurnitureDefinitionEntity definition) =>
        definition.ProductType == ProductType.Wall ? "wall" : "floor";

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

        List<GamedataChangeEntity> changes = [];
        var changeSet = GamedataChangeSets.Create(
            GamedataChangeKind.HabboValues,
            $"Habbo's {string.Join(", ", chosen.Select(x => x.Key))} ({habbo!.Release.Revision}) for {differences.Count} furniture",
            player,
            changes
        );

        foreach (var (definition, keys, item) in differences)
        {
            var before = FurnitureRecords.Fields(definition, keys);

            foreach (var key in keys)
                FurnitureFields.ByKey[key].Write(definition, item[key]);

            changes.Add(
                GamedataChangeSets.Change(
                    GamedataRecordType.FurnitureDefinition,
                    definition.Name,
                    before.ToJsonString(),
                    FurnitureRecords.Fields(definition, keys).ToJsonString(),
                    definition.Id
                )
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

        return changeSet.ToSnapshot(changes.Count);
    }

    public Task<GamedataChangeSetSnapshot?> SetLogicAsync(
        IReadOnlyCollection<int>? ids,
        string? logic,
        PlayerId player,
        CancellationToken ct
    ) => writes.RunAsync(() => SetLogicLockedAsync(ids, logic, player, ct), ct);

    private async Task<GamedataChangeSetSnapshot?> SetLogicLockedAsync(
        IReadOnlyCollection<int>? ids,
        string? logic,
        PlayerId player,
        CancellationToken ct
    )
    {
        if (ids is null && logic is not null)
            throw new ArgumentException("Choose the furniture to give that logic.", nameof(ids));

        if (logic is not null && !logics.LogicTypes.Contains(logic, StringComparer.Ordinal))
            throw new ArgumentException($"{logic} is not a logic the server has.", nameof(logic));

        var chosen = ids?.ToHashSet();
        var wanted = (await statuses.GetAsync(ct).ConfigureAwait(false))
            .Where(x =>
                chosen is null ? x.Tags.Contains(FurnitureTag.LogicMismatch) : chosen.Contains(x.Id)
            )
            .Select(x => (x.Id, Logic: logic ?? x.MatchingLogic))
            .Where(x => x.Logic is not null && x.Logic.Length > 0)
            .ToDictionary(x => x.Id, x => x.Logic!);
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var definitions = (
            await dbCtx
                .FurnitureDefinitions.Where(x => wanted.Keys.Contains(x.Id))
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            .Where(x => x.Logic != wanted[x.Id])
            .OrderBy(x => x.Id)
            .ToList();

        if (definitions.Count == 0)
            return null;

        var field = FurnitureFields.ByKey[FurnitureRecords.LOGIC_KEY];
        string[] keys = [FurnitureRecords.LOGIC_KEY];
        List<GamedataChangeEntity> changes = [];
        var changeSet = GamedataChangeSets.Create(
            GamedataChangeKind.Edit,
            logic is null
                ? $"Matching logic for {definitions.Count} furniture"
                : $"Logic {logic} for {definitions.Count} furniture",
            player,
            changes
        );

        foreach (var definition in definitions)
        {
            var before = FurnitureRecords.Fields(definition, keys);

            field.Write(definition, field.Normalize(JsonValue.Create(wanted[definition.Id])));
            changes.Add(
                GamedataChangeSets.Change(
                    GamedataRecordType.FurnitureDefinition,
                    definition.Name,
                    before.ToJsonString(),
                    FurnitureRecords.Fields(definition, keys).ToJsonString(),
                    definition.Id
                )
            );
        }

        dbCtx.GamedataChangeSets.Add(changeSet);

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Player {PlayerId} set the logic of {Count} furniture definitions: {Changes}",
            player.Value,
            definitions.Count,
            string.Join(", ", definitions.Select(x => $"{x.Name}={x.Logic}"))
        );

        await AfterWriteAsync(ct).ConfigureAwait(false);

        return changeSet.ToSnapshot(changes.Count);
    }

    /// <summary>The fields by their furnidata keys, each once; a key no definition holds is refused.</summary>
    private static List<FurnitureField> Resolve(IReadOnlyCollection<string> keys)
    {
        var fields = new List<FurnitureField>();

        foreach (var key in keys.Select(x => x.Trim()).Distinct())
            fields.Add(
                FurnitureFields.ByKey.TryGetValue(key, out var field) && !field.ServerOnly
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

        var listed = (await statuses.GetAsync(ct).ConfigureAwait(false)).FirstOrDefault(x =>
            x.Id == definition.Id
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
            AssetName = HabboFurnitureFiles.AssetName(definition.Name),
            ServerFields = FurnitureRecords
                .Fields(definition, FurnitureFields.All.Where(x => x.ServerOnly).Select(x => x.Key))
                .ToJsonString(),
            Tags = listed?.Tags ?? [],
            MatchingLogic = listed?.MatchingLogic,
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

    /// <summary>Every item of the release, matched to the hotel's definition of it and to Habbo's last version of it.</summary>
    private async Task<ImportPlan> PlanAsync(
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
        var registered = logics.LogicTypes.ToHashSet(StringComparer.Ordinal);
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

                // With what its asset file said (its states), when the file was read. The release
                // was parsed for this plan alone, so its items take that as they are, uncopied.
                var habbo = HabboFurnitureFiles.EnrichInPlace(item, files);
                var spriteId = FurnitureValues.ToInt(habbo["id"], "id");

                plan.HabboSpriteIds(type).Add(spriteId);

                byName.TryGetValue((type, className), out var definition);
                bases.TryGetValue((type, className), out var known);

                // Habbo's last version is read only for an item whose fields differ.
                var fields = definition is null
                    ? []
                    : FurnitureMerge.CompareLazily(
                        definition,
                        habbo,
                        new Lazy<JsonObject?>(
                            () => known is null ? null : JsonNode.Parse(known.Data) as JsonObject,
                            LazyThreadSafetyMode.None
                        )
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
                        action,
                        // Only a definition being made gets a logic: an existing one keeps the
                        // logic staff gave it.
                        definition is null
                            ? FurnitureBehaviourApplier.LogicOf(
                                type,
                                className,
                                FurnitureDrafts.LogicOf(
                                    type,
                                    className,
                                    HabboFurnitureFiles.ClientLogicOf(habbo, files),
                                    habbo["category"]?.GetValue<string>(),
                                    registered
                                )
                            )
                            : null
                    )
                );
            }
        }

        return plan;
    }

    private static GamedataChangeEntity Change(string className, GamedataRecordType type) =>
        GamedataChangeSets.Change(type, className, null, null);

    private sealed record PlannedItem(
        ProductType ProductType,
        string ClassName,
        int SpriteId,
        JsonObject Habbo,
        FurnitureDefinitionEntity? Definition,
        HabboFurnitureEntity? Base,
        ImmutableArray<FurnitureFieldChange> Fields,
        FurnitureImportAction? Action,
        string? Logic
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
                Logic = Logic,
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
