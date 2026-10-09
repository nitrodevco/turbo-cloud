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
using Turbo.Database.Entities.Gamedata;
using Turbo.Database.Extensions;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Figures;
using Turbo.Gamedata.Furniture;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Texts;

namespace Turbo.Gamedata.History;

/// <summary>
/// The history of gamedata changes, and rolling a set back (<see cref="IGamedataHistoryService"/>).
/// A rollback undoes only what is still as the set left it: a field changed again since keeps the
/// later value, and a definition the set made stays when furniture, an offer or a Builders Club
/// placement uses it now. Each such thing is reported as skipped. A rollback is recorded as a set
/// of its own, but it can't be rolled back in turn: what it removed is gone; import or edit again.
/// </summary>
internal sealed class GamedataHistoryService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config,
    IFurnitureDefinitionProvider definitions,
    IGamedataFileService files,
    GamedataWriteLock writes,
    IHotelTextProvider hotelTexts,
    IFigureDataProvider figureData,
    ILogger<GamedataHistoryService> logger
) : IGamedataHistoryService
{
    private readonly GamedataConfig _config = config.Value;

    public async Task<ImmutableArray<GamedataChangeSetSnapshot>> ListAsync(
        int page,
        CancellationToken ct
    )
    {
        var size = Math.Max(1, _config.HistoryPageSize);

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var sets = await dbCtx
            .GamedataChangeSets.AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Skip(Math.Max(0, page) * size)
            .Take(size)
            .Select(x => new { Set = x, Count = x.Changes!.Count })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. sets.Select(x => x.Set.ToSnapshot(x.Count))];
    }

    public async Task<ImmutableArray<GamedataChangeSnapshot>?> GetChangesAsync(
        int changeSetId,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        if (
            !await dbCtx
                .GamedataChangeSets.AnyAsync(x => x.Id == changeSetId, ct)
                .ConfigureAwait(false)
        )
            return null;

        var changes = await dbCtx
            .GamedataChanges.AsNoTracking()
            .Where(x => x.ChangeSetEntityId == changeSetId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. changes.Select(x => x.ToSnapshot())];
    }

    public Task<GamedataRollbackResult?> RollbackAsync(
        int changeSetId,
        PlayerId player,
        CancellationToken ct
    ) => writes.RunAsync(() => RollbackLockedAsync(changeSetId, player, ct), ct);

    private async Task<GamedataRollbackResult?> RollbackLockedAsync(
        int changeSetId,
        PlayerId player,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var set = await dbCtx
            .GamedataChangeSets.Include(x => x.Changes)
            .FirstOrDefaultAsync(x => x.Id == changeSetId, ct)
            .ConfigureAwait(false);

        if (set is null)
            return null;

        if (set.Kind == GamedataChangeKind.Rollback)
            throw new InvalidOperationException(
                "A rollback can't be rolled back: what it removed is gone. Import or edit again instead."
            );

        if (set.RolledBackByEntityId is { } by)
            throw new InvalidOperationException(
                $"Change set {set.Id} was rolled back already, by {by}."
            );

        var changes = set.Changes!.OrderByDescending(x => x.Id).ToList();
        var definitionIds = changes
            .Where(x => x.RecordType == GamedataRecordType.FurnitureDefinition)
            .Select(x => x.RecordId)
            .ToList();
        var habboIds = changes
            .Where(x => x.RecordType == GamedataRecordType.HabboFurniture)
            .Select(x => x.RecordId)
            .ToList();
        var definitionRows = await dbCtx
            .FurnitureDefinitions.Where(x => definitionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct)
            .ConfigureAwait(false);
        var habboRows = await dbCtx
            .HabboFurniture.Where(x => habboIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct)
            .ConfigureAwait(false);
        var made = changes
            .Where(x => x.RecordType == GamedataRecordType.FurnitureDefinition && x.Before is null)
            .Select(x => x.RecordId)
            .ToList();
        var inUse = await InUseAsync(dbCtx, made, ct).ConfigureAwait(false);
        var textKeys = changes
            .Where(x => x.RecordType is GamedataRecordType.Text or GamedataRecordType.HabboText)
            .Select(x => TextKey(x))
            .OfType<string>()
            .Distinct()
            .ToList();
        var texts = await dbCtx
            .GamedataTexts.Where(x => textKeys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);
        var productCodes = changes
            .Where(x =>
                x.RecordType is GamedataRecordType.Product or GamedataRecordType.HabboProduct
            )
            .Select(x => ProductRecord(x.Before)?.Code ?? ProductRecord(x.After)?.Code)
            .OfType<string>()
            .Distinct()
            .ToList();
        var products = await dbCtx
            .GamedataProducts.Where(x => productCodes.Contains(x.Code))
            .ToDictionaryAsync(x => x.Code, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);
        var habboProducts = await dbCtx
            .HabboProducts.Where(x => productCodes.Contains(x.Code))
            .ToDictionaryAsync(x => x.Code, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);
        var habboTexts = await dbCtx
            .HabboTexts.Where(x => textKeys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);
        var figureKeys = changes
            .Where(x => x.RecordType is GamedataRecordType.Figure or GamedataRecordType.HabboFigure)
            .Select(x => FigureIdentity(x)?.Key)
            .OfType<string>()
            .Distinct()
            .ToList();
        var figures = (
            await dbCtx
                .GamedataFigures.Where(x => figureKeys.Contains(x.Key))
                .ToListAsync(ct)
                .ConfigureAwait(false)
        ).ToDictionary(x => (x.Kind, x.Key));
        var habboFigures = (
            await dbCtx
                .HabboFigures.Where(x => figureKeys.Contains(x.Key))
                .ToListAsync(ct)
                .ConfigureAwait(false)
        ).ToDictionary(x => (x.Kind, x.Key));

        var variableKeys = changes
            .Where(x => x.RecordType == GamedataRecordType.Variable)
            .Select(x => TextKey(x))
            .OfType<string>()
            .Distinct()
            .ToList();
        var variables = await dbCtx
            .GamedataVariables.Where(x => variableKeys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);

        var skipped = new List<string>();
        var reverts = new List<GamedataChangeEntity>();

        foreach (var change in changes)
        {
            var revert = change.RecordType switch
            {
                GamedataRecordType.FurnitureDefinition => RevertDefinition(
                    dbCtx,
                    change,
                    definitionRows,
                    inUse,
                    skipped
                ),
                GamedataRecordType.HabboFurniture => RevertHabbo(dbCtx, change, habboRows, skipped),
                GamedataRecordType.Text => RevertText(dbCtx, change, texts, skipped),
                GamedataRecordType.HabboText => RevertHabboText(dbCtx, change, habboTexts, skipped),
                GamedataRecordType.Product => RevertProduct(dbCtx, change, products, skipped),
                GamedataRecordType.HabboProduct => RevertHabboProduct(
                    dbCtx,
                    change,
                    habboProducts,
                    skipped
                ),
                GamedataRecordType.Figure => RevertFigure(dbCtx, change, figures, skipped),
                GamedataRecordType.HabboFigure => RevertHabboFigure(
                    dbCtx,
                    change,
                    habboFigures,
                    skipped
                ),
                GamedataRecordType.Variable => RevertVariable(dbCtx, change, variables, skipped),
                _ => null,
            };

            if (revert is not null)
                reverts.Add(revert);
        }

        var rollback = new GamedataChangeSetEntity
        {
            Kind = GamedataChangeKind.Rollback,
            Summary = Truncate(
                $"Rolled back #{set.Id}: {set.Summary}",
                GamedataChangeSetEntity.SUMMARY_MAX_LENGTH
            ),
            PlayerEntityId = player.Value,
            RevertsEntityId = set.Id,
            Changes = reverts,
        };

        var tx = await dbCtx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        await using var txScope = tx.ConfigureAwait(false);

        dbCtx.GamedataChangeSets.Add(rollback);

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        set.RolledBackByEntityId = rollback.Id;

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Player {PlayerId} rolled back gamedata change set {ChangeSetId}: {Reverted} rows reverted, {Skipped} things skipped",
            player.Value,
            set.Id,
            reverts.Count,
            skipped.Count
        );

        foreach (var file in GamedataFiles.ALL)
            files.Invalidate(file);

        hotelTexts.Invalidate();
        figureData.Invalidate();

        try
        {
            await definitions.ReloadAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Reloading furniture definitions after a rollback failed");
        }

        return new GamedataRollbackResult
        {
            ChangeSet = rollback.ToSnapshot(reverts.Count),
            Skipped = [.. skipped],
        };
    }

    private static GamedataChangeEntity? RevertDefinition(
        TurboDbContext dbCtx,
        GamedataChangeEntity change,
        Dictionary<int, Database.Entities.Furniture.FurnitureDefinitionEntity> rows,
        IReadOnlySet<int> inUse,
        List<string> skipped
    )
    {
        if (!rows.TryGetValue(change.RecordId, out var definition))
        {
            skipped.Add($"{change.Label}: the definition is gone.");

            return null;
        }

        // A definition the set made goes, unless something uses it now.
        if (change.Before is null)
        {
            if (inUse.Contains(definition.Id))
            {
                skipped.Add(
                    $"{change.Label}: kept, because furniture, an offer or a Builders Club placement uses it."
                );

                return null;
            }

            dbCtx.FurnitureDefinitions.Remove(definition);

            return new GamedataChangeEntity
            {
                RecordType = GamedataRecordType.FurnitureDefinition,
                RecordId = definition.Id,
                Label = change.Label,
                Before = FurnitureRecords.Created(definition).ToJsonString(),
                After = null,
            };
        }

        var before = JsonNode.Parse(change.Before) as JsonObject ?? [];
        var after = change.After is null ? [] : JsonNode.Parse(change.After) as JsonObject ?? [];
        var reverted = new List<string>();

        foreach (var (key, value) in before)
        {
            if (!FurnitureFields.ByKey.TryGetValue(key, out var field))
                continue;

            if (!FurnitureField.Same(field.Read(definition), field.Normalize(after[key])))
            {
                skipped.Add($"{change.Label}: {key} has changed again since.");

                continue;
            }

            reverted.Add(key);
        }

        if (reverted.Count == 0)
            return null;

        var undone = FurnitureRecords.Fields(definition, reverted);

        foreach (var key in reverted)
            FurnitureFields.ByKey[key].Write(definition, before[key]);

        return new GamedataChangeEntity
        {
            RecordType = GamedataRecordType.FurnitureDefinition,
            RecordId = definition.Id,
            Label = change.Label,
            Before = undone.ToJsonString(),
            After = FurnitureRecords.Fields(definition, reverted).ToJsonString(),
        };
    }

    private sealed record ProductValues(string Code, string? Name, string? Description);

    private static ProductValues? ProductRecord(string? json) =>
        json is not null
        && JsonNode.Parse(json) is JsonObject record
        && record["code"]?.GetValue<string>() is { } code
            ? new ProductValues(
                code,
                record["name"]?.GetValue<string>(),
                record["description"]?.GetValue<string>()
            )
            : null;

    /// <summary>A product back as it was before the set, when it is still as the set left it.</summary>
    private static GamedataChangeEntity? RevertProduct(
        TurboDbContext dbCtx,
        GamedataChangeEntity change,
        Dictionary<string, GamedataProductEntity> rows,
        List<string> skipped
    )
    {
        var before = ProductRecord(change.Before);
        var after = ProductRecord(change.After);
        var code = before?.Code ?? after?.Code;

        if (code is null)
            return null;

        rows.TryGetValue(code, out var row);

        var current = row is null ? null : new ProductValues(code, row.Name, row.Description);

        if (current != after)
        {
            skipped.Add($"{code}: the product has changed again since.");

            return null;
        }

        if (before is null)
        {
            dbCtx.GamedataProducts.Remove(row!);
            rows.Remove(code);
        }
        else if (row is null)
        {
            row = new GamedataProductEntity
            {
                Code = code,
                Name = before.Name,
                Description = before.Description,
            };
            dbCtx.GamedataProducts.Add(row);
            rows[code] = row;
        }
        else
        {
            row.Name = before.Name;
            row.Description = before.Description;
        }

        return new GamedataChangeEntity
        {
            RecordType = GamedataRecordType.Product,
            RecordId = change.RecordId,
            Label = change.Label,
            Before = change.After,
            After = change.Before,
        };
    }

    /// <summary>Habbo's product as last taken in back as it was, when no later import changed it.</summary>
    private static GamedataChangeEntity? RevertHabboProduct(
        TurboDbContext dbCtx,
        GamedataChangeEntity change,
        Dictionary<string, HabboProductEntity> rows,
        List<string> skipped
    )
    {
        var before = ProductRecord(change.Before);
        var after = ProductRecord(change.After);
        var code = before?.Code ?? after?.Code;

        if (code is null || !rows.TryGetValue(code, out var row))
            return null;

        if (new ProductValues(code, row.Name, row.Description) != after)
        {
            skipped.Add($"{code}: Habbo's product was taken in again since.");

            return null;
        }

        if (before is null)
        {
            dbCtx.HabboProducts.Remove(row);
        }
        else
        {
            row.Name = before.Name;
            row.Description = before.Description;
        }

        return new GamedataChangeEntity
        {
            RecordType = GamedataRecordType.HabboProduct,
            RecordId = change.RecordId,
            Label = change.Label,
            Before = change.After,
            After = change.Before,
        };
    }

    /// <summary>A figure change's kind and key, from whichever of its records it has.</summary>
    private static (FigureRecordKind Kind, string Key)? FigureIdentity(GamedataChangeEntity change)
    {
        var json = change.Before ?? change.After;

        if (json is null || JsonNode.Parse(json) is not JsonObject record)
            return null;

        try
        {
            var kind = GamedataFigureService.KindOf(record);

            return (kind, FigureRecords.KeyOf(kind, record));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// A figure record back as it was before the set - made, changed or removed - when it is
    /// still as the set left it.
    /// </summary>
    private static GamedataChangeEntity? RevertFigure(
        TurboDbContext dbCtx,
        GamedataChangeEntity change,
        Dictionary<(FigureRecordKind, string), GamedataFigureEntity> rows,
        List<string> skipped
    )
    {
        if (FigureIdentity(change) is not { } identity)
            return null;

        rows.TryGetValue(identity, out var row);

        if (row?.Data != change.After)
        {
            skipped.Add($"{change.Label}: it has changed again since.");

            return null;
        }

        if (change.Before is null)
        {
            dbCtx.GamedataFigures.Remove(row!);
            rows.Remove(identity);
        }
        else
        {
            var group = FigureRecords.GroupOf(identity.Kind, FigureRecords.Parse(change.Before));

            if (row is null)
            {
                row = new GamedataFigureEntity
                {
                    Kind = identity.Kind,
                    Key = identity.Key,
                    Group = group,
                    Data = change.Before,
                };
                dbCtx.GamedataFigures.Add(row);
                rows[identity] = row;
            }
            else
            {
                row.Data = change.Before;
                row.Group = group;
            }
        }

        return new GamedataChangeEntity
        {
            RecordType = GamedataRecordType.Figure,
            RecordId = change.RecordId,
            Label = change.Label,
            Before = change.After,
            After = change.Before,
        };
    }

    /// <summary>Habbo's figure record as last taken in back as it was, when no later import changed it.</summary>
    private static GamedataChangeEntity? RevertHabboFigure(
        TurboDbContext dbCtx,
        GamedataChangeEntity change,
        Dictionary<(FigureRecordKind, string), HabboFigureEntity> rows,
        List<string> skipped
    )
    {
        if (FigureIdentity(change) is not { } identity || !rows.TryGetValue(identity, out var row))
            return null;

        if (row.Data != change.After)
        {
            skipped.Add($"{change.Label}: Habbo's was taken in again since.");

            return null;
        }

        if (change.Before is null)
            dbCtx.HabboFigures.Remove(row);
        else
            row.Data = change.Before;

        return new GamedataChangeEntity
        {
            RecordType = GamedataRecordType.HabboFigure,
            RecordId = change.RecordId,
            Label = change.Label,
            Before = change.After,
            After = change.Before,
        };
    }

    /// <summary>
    /// An external variable back as it was before the set - made, changed or removed - when it
    /// is still as the set left it. Its records are a text's: <c>{ key, value }</c>.
    /// </summary>
    private static GamedataChangeEntity? RevertVariable(
        TurboDbContext dbCtx,
        GamedataChangeEntity change,
        Dictionary<string, GamedataVariableEntity> rows,
        List<string> skipped
    )
    {
        var before = VariableRecord(change.Before);
        var after = VariableRecord(change.After);
        var key = before?.Key ?? after?.Key;

        if (key is null)
            return null;

        rows.TryGetValue(key, out var row);

        if (
            row?.Value != after?.Value
            || row?.SettingPath != after?.Setting
            || row?.LinkedFile != after?.File
        )
        {
            skipped.Add($"{key}: the variable has changed again since.");

            return null;
        }

        if (before is null)
        {
            dbCtx.GamedataVariables.Remove(row!);
            rows.Remove(key);
        }
        else if (row is null)
        {
            row = new GamedataVariableEntity
            {
                Key = key,
                Value = before.Value.Value,
                SettingPath = before.Value.Setting,
                LinkedFile = before.Value.File,
            };
            dbCtx.GamedataVariables.Add(row);
            rows[key] = row;
        }
        else
        {
            row.Value = before.Value.Value;
            row.SettingPath = before.Value.Setting;
            row.LinkedFile = before.Value.File;
        }

        return new GamedataChangeEntity
        {
            RecordType = GamedataRecordType.Variable,
            RecordId = change.RecordId,
            Label = change.Label,
            Before = change.After,
            After = change.Before,
        };
    }

    /// <summary>A variable's record: a text's, and the setting or file it follows when it follows one.</summary>
    private static (string Key, string Value, string? Setting, string? File)? VariableRecord(
        string? json
    ) =>
        TextRecord(json) is { } text && JsonNode.Parse(json!) is { } record
            ? (
                text.Key,
                text.Value,
                record["setting"]?.GetValue<string>(),
                record["file"]?.GetValue<string>()
            )
            : null;

    /// <summary>A text or variable change's key, from whichever of its records it has.</summary>
    private static string? TextKey(GamedataChangeEntity change) =>
        TextRecord(change.Before)?.Key ?? TextRecord(change.After)?.Key;

    private static (string Key, string Value)? TextRecord(string? json) =>
        json is not null
        && JsonNode.Parse(json) is JsonObject record
        && record["key"]?.GetValue<string>() is { } key
            ? (key, record["value"]?.GetValue<string>() ?? string.Empty)
            : null;

    /// <summary>
    /// A text back as it was before the set - made, changed or removed - when it is still as the
    /// set left it.
    /// </summary>
    private static GamedataChangeEntity? RevertText(
        TurboDbContext dbCtx,
        GamedataChangeEntity change,
        Dictionary<string, GamedataTextEntity> rows,
        List<string> skipped
    )
    {
        var before = TextRecord(change.Before);
        var after = TextRecord(change.After);
        var key = before?.Key ?? after?.Key;

        if (key is null)
            return null;

        rows.TryGetValue(key, out var row);

        if (row?.Value != after?.Value)
        {
            skipped.Add($"{key}: the text has changed again since.");

            return null;
        }

        if (before is null)
        {
            dbCtx.GamedataTexts.Remove(row!);
            rows.Remove(key);
        }
        else if (row is null)
        {
            row = new GamedataTextEntity { Key = key, Value = before.Value.Value };
            dbCtx.GamedataTexts.Add(row);
            rows[key] = row;
        }
        else
        {
            row.Value = before.Value.Value;
        }

        return new GamedataChangeEntity
        {
            RecordType = GamedataRecordType.Text,
            RecordId = change.RecordId,
            Label = change.Label,
            Before = change.After,
            After = change.Before,
        };
    }

    /// <summary>Habbo's text as last taken in back as it was, when no later import changed it.</summary>
    private static GamedataChangeEntity? RevertHabboText(
        TurboDbContext dbCtx,
        GamedataChangeEntity change,
        Dictionary<string, HabboTextEntity> rows,
        List<string> skipped
    )
    {
        var before = TextRecord(change.Before);
        var after = TextRecord(change.After);
        var key = before?.Key ?? after?.Key;

        if (key is null || !rows.TryGetValue(key, out var row))
            return null;

        if (row.Value != after?.Value)
        {
            skipped.Add($"{key}: Habbo's text was taken in again since.");

            return null;
        }

        if (before is null)
            dbCtx.HabboTexts.Remove(row);
        else
            row.Value = before.Value.Value;

        return new GamedataChangeEntity
        {
            RecordType = GamedataRecordType.HabboText,
            RecordId = change.RecordId,
            Label = change.Label,
            Before = change.After,
            After = change.Before,
        };
    }

    private static GamedataChangeEntity? RevertHabbo(
        TurboDbContext dbCtx,
        GamedataChangeEntity change,
        Dictionary<int, HabboFurnitureEntity> rows,
        List<string> skipped
    )
    {
        if (!rows.TryGetValue(change.RecordId, out var row))
            return null;

        // A later import took in a newer version of the item: that one stays the base.
        if (row.Data != change.After)
        {
            skipped.Add($"{change.Label}: Habbo's item was taken in again since.");

            return null;
        }

        if (change.Before is null)
            dbCtx.HabboFurniture.Remove(row);
        else
            row.Data = change.Before;

        return new GamedataChangeEntity
        {
            RecordType = GamedataRecordType.HabboFurniture,
            RecordId = row.Id,
            Label = change.Label,
            Before = change.After,
            After = change.Before,
        };
    }

    /// <summary>Which of these definitions something uses: furniture, an offer, a Builders Club placement.</summary>
    private static async Task<IReadOnlySet<int>> InUseAsync(
        TurboDbContext dbCtx,
        List<int> ids,
        CancellationToken ct
    )
    {
        if (ids.Count == 0)
            return new HashSet<int>();

        var used = new HashSet<int>();

        used.UnionWith(
            await dbCtx
                .Furnitures.Where(x => ids.Contains(x.FurnitureDefinitionEntityId))
                .Select(x => x.FurnitureDefinitionEntityId)
                .Distinct()
                .ToListAsync(ct)
                .ConfigureAwait(false)
        );
        used.UnionWith(
            await dbCtx
                .CatalogProducts.Where(x =>
                    x.FurnitureDefinitionEntityId != null
                    && ids.Contains(x.FurnitureDefinitionEntityId.Value)
                )
                .Select(x => x.FurnitureDefinitionEntityId!.Value)
                .Distinct()
                .ToListAsync(ct)
                .ConfigureAwait(false)
        );
        used.UnionWith(
            await dbCtx
                .BuildersClubFurnitures.Where(x => ids.Contains(x.FurnitureDefinitionEntityId))
                .Select(x => x.FurnitureDefinitionEntityId)
                .Distinct()
                .ToListAsync(ct)
                .ConfigureAwait(false)
        );

        return used;
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
