using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
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
using Turbo.Gamedata.Texts;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata.Figures;

/// <summary>
/// The hotel's figure data (<see cref="IGamedataFigureService"/>). An import compares each of
/// Habbo's records field by field three ways, as a product is: Habbo's new value, Habbo's when
/// last taken in (<c>habbo_figure_records</c>) and the hotel's (<c>gamedata_figure_records</c>). A
/// record the hotel removed stays removed unless Habbo changes it; one Habbo drops stays. A change
/// records the whole record before and after.
/// </summary>
internal sealed class GamedataFigureService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config,
    IGamedataFileService files,
    IFigureDataProvider figureData,
    GamedataWriteLock writes,
    TimeProvider time,
    ILogger<GamedataFigureService> logger
) : IGamedataFigureService
{
    private readonly GamedataConfig _config = config.Value;

    public async Task<HabboFigureVersionSnapshot?> GetLatestVersionAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var latest = await LatestQuery(dbCtx)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return latest?.ToSnapshot();
    }

    public async Task<FigureImportPreview?> PreviewImportAsync(int? versionId, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var version = await FindVersionAsync(dbCtx, versionId, ct).ConfigureAwait(false);

        if (version is null)
            return null;

        var plan = await PlanAsync(dbCtx, version, tracked: false, ct).ConfigureAwait(false);
        var touched = plan.Where(x => x.Action is not null).ToList();

        return new FigureImportPreview
        {
            Version = version.ToSnapshot(),
            Added = plan.Count(x => x.Action == FurnitureImportAction.Add),
            Updated = plan.Count(x => x.Action == FurnitureImportAction.Update),
            Kept = plan.Count(x => x.Action == FurnitureImportAction.Keep),
            Unchanged = plan.Count(x => x.Action is null),
            Items =
            [
                .. touched
                    .Take(_config.PreviewItemLimit)
                    .Select(x => new FigureImportItem
                    {
                        Kind = x.Kind,
                        Key = x.Key,
                        Action = x.Action!.Value,
                        Fields = x.Fields,
                    }),
            ],
            Truncated = touched.Count > _config.PreviewItemLimit,
        };
    }

    public Task<GamedataChangeSetSnapshot?> ImportAsync(
        int versionId,
        PlayerId? player,
        CancellationToken ct
    ) => writes.RunAsync(() => ImportLockedAsync(versionId, player, ct), ct);

    private async Task<GamedataChangeSetSnapshot?> ImportLockedAsync(
        int versionId,
        PlayerId? player,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var version = await FindVersionAsync(dbCtx, versionId, ct).ConfigureAwait(false);

        if (version is null)
            return null;

        var plan = await PlanAsync(dbCtx, version, tracked: true, ct).ConfigureAwait(false);
        var changes = new List<(GamedataChangeEntity Change, object Row)>();
        var added = 0;
        var updated = 0;

        dbCtx.ChangeTracker.AutoDetectChangesEnabled = false;

        var tx = await dbCtx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var txScope = tx.ConfigureAwait(false);

        foreach (var item in plan)
        {
            var habbo = FigureRecords.Json(item.Habbo);

            if (item.Action == FurnitureImportAction.Add)
            {
                var row = new GamedataFigureEntity
                {
                    Kind = item.Kind,
                    Key = item.Key,
                    Group = FigureRecords.GroupOf(item.Kind, item.Habbo),
                    Data = habbo,
                };

                dbCtx.GamedataFigures.Add(row);
                changes.Add((Change(GamedataRecordType.Figure, item, null, habbo), row));
                added++;
            }
            else if (item.Action == FurnitureImportAction.Update)
            {
                var row = item.Current!;
                var before = row.Data;
                var record = FigureRecords.Parse(row.Data);

                foreach (var field in item.Fields.Where(x => !x.Kept))
                    record[field.Field] = item.Habbo[field.Field]?.DeepClone();

                record = FigureRecords.Normalize(item.Kind, record);
                row.Data = FigureRecords.Json(record);
                row.Group = FigureRecords.GroupOf(item.Kind, record);
                dbCtx.Entry(row).State = EntityState.Modified;
                changes.Add((Change(GamedataRecordType.Figure, item, before, row.Data), row));
                updated++;
            }

            if (item.Base is { } known)
            {
                if (known.Data == habbo)
                    continue;

                changes.Add(
                    (Change(GamedataRecordType.HabboFigure, item, known.Data, habbo), known)
                );
                known.Data = habbo;
                known.HabboFigureVersionEntityId = version.Id;
                dbCtx.Entry(known).State = EntityState.Modified;
            }
            else
            {
                var row = new HabboFigureEntity
                {
                    Kind = item.Kind,
                    Key = item.Key,
                    Data = habbo,
                    HabboFigureVersionEntityId = version.Id,
                };

                dbCtx.HabboFigures.Add(row);
                changes.Add((Change(GamedataRecordType.HabboFigure, item, null, habbo), row));
            }
        }

        version.ImportedAt = time.GetUtcNow().UtcDateTime;
        dbCtx.Entry(version).State = EntityState.Modified;

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        GamedataChangeSetEntity? changeSet = null;

        if (changes.Count > 0)
        {
            var kept = plan.Count(x => x.Action == FurnitureImportAction.Keep);

            changeSet = new GamedataChangeSetEntity
            {
                Kind = GamedataChangeKind.Import,
                Summary = Truncate(
                    $"Habbo's figure data ({version.SetCount} pieces, {version.ColorCount} colours): {added} added, {updated} updated, {kept} kept as the hotel has them",
                    GamedataChangeSetEntity.SUMMARY_MAX_LENGTH
                ),
                PlayerEntityId = player?.Value,
                Changes = [],
            };

            foreach (var (change, row) in changes)
            {
                change.RecordId = row switch
                {
                    GamedataFigureEntity figure => figure.Id,
                    HabboFigureEntity habbo => habbo.Id,
                    _ => 0,
                };
                changeSet.Changes.Add(change);
            }

            dbCtx.GamedataChangeSets.Add(changeSet);

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Imported Habbo's figure data {VersionId}: {Added} added, {Updated} updated",
            version.Id,
            added,
            updated
        );

        Changed();

        return changeSet?.ToSnapshot(changeSet.Changes!.Count);
    }

    public async Task<FigureSearchResult> SearchAsync(
        FigureRecordKind kind,
        string? group,
        string? query,
        IReadOnlyList<string> has,
        int page,
        CancellationToken ct
    )
    {
        var size = Math.Max(1, _config.TextPageSize);
        var words = (query ?? string.Empty).Trim();
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var records = dbCtx.GamedataFigures.AsNoTracking().Where(x => x.Kind == kind);

        if (!string.IsNullOrWhiteSpace(group))
            records = records.Where(x => x.Group == group.Trim());

        if (words.Length > 0)
            records = records.Where(x => x.Key.Contains(words) || x.Data.Contains(words));

        foreach (var field in has)
            if (HasAny(field.Split('|', StringSplitOptions.RemoveEmptyEntries)) is { } filter)
                records = records.Where(filter);

        var total = await records.CountAsync(ct).ConfigureAwait(false);
        var rows = await records
            .OrderBy(x => x.Group.Length)
            .ThenBy(x => x.Group)
            .ThenBy(x => x.Key.Length)
            .ThenBy(x => x.Key)
            .Skip(Math.Max(0, page) * size)
            .Take(size)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new FigureSearchResult
        {
            Items = await EntriesAsync(dbCtx, rows, ct).ConfigureAwait(false),
            Total = total,
            PageSize = size,
        };
    }

    /// <summary>A record whose JSON holds any of these fragments, as the database can ask it.</summary>
    private static Expression<Func<GamedataFigureEntity, bool>>? HasAny(string[] fragments)
    {
        var record = Expression.Parameter(typeof(GamedataFigureEntity), "x");
        var data = Expression.Property(record, nameof(GamedataFigureEntity.Data));
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        Expression? any = null;

        foreach (var fragment in fragments)
        {
            Expression one = Expression.Call(data, contains, Expression.Constant(fragment));

            any = any is null ? one : Expression.OrElse(any, one);
        }

        return any is null
            ? null
            : Expression.Lambda<Func<GamedataFigureEntity, bool>>(any, record);
    }

    public async Task<FigureKindsSnapshot> GetKindsAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var kinds = await dbCtx
            .GamedataFigures.AsNoTracking()
            .Where(x => x.Kind == FigureRecordKind.SetType)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var pieces = await dbCtx
            .GamedataFigures.AsNoTracking()
            .Where(x => x.Kind == FigureRecordKind.Set)
            .Select(x => new { x.Group, x.Key })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var counts = pieces
            .GroupBy(x => x.Group, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        var highest = pieces
            .Select(x =>
                int.TryParse(x.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                    ? id
                    : 0
            )
            .DefaultIfEmpty(0)
            .Max();
        var entries = await EntriesAsync(dbCtx, kinds, ct).ConfigureAwait(false);

        return new FigureKindsSnapshot
        {
            Kinds =
            [
                .. entries
                    .OrderBy(x => FigureDataFile.Order(x.Key))
                    .ThenBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => new FigureKindSnapshot
                    {
                        Entry = x,
                        Pieces = counts.GetValueOrDefault(x.Key),
                    }),
            ],
            NextSetId = highest + 1,
        };
    }

    public Task<FigureEntrySnapshot> SaveAsync(
        FigureRecordKind kind,
        string data,
        PlayerId player,
        CancellationToken ct
    ) => writes.RunAsync(() => SaveLockedAsync(kind, data, player, ct), ct);

    private async Task<FigureEntrySnapshot> SaveLockedAsync(
        FigureRecordKind kind,
        string data,
        PlayerId player,
        CancellationToken ct
    )
    {
        JsonObject record;

        try
        {
            record = FigureRecords.Normalize(kind, FigureRecords.Parse(data));
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new ArgumentException("A figure record is a JSON object.", nameof(data), ex);
        }

        var key = FigureRecords.KeyOf(kind, record);

        if (key.Length > GamedataFigureEntity.KEY_MAX_LENGTH)
            throw new ArgumentException("That record's key is too long.", nameof(data));

        var json = FigureRecords.Json(record);
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .GamedataFigures.FirstOrDefaultAsync(x => x.Kind == kind && x.Key == key, ct)
            .ConfigureAwait(false);
        var before = row?.Data;

        if (row is null)
        {
            row = new GamedataFigureEntity
            {
                Kind = kind,
                Key = key,
                Group = FigureRecords.GroupOf(kind, record),
                Data = json,
            };
            dbCtx.GamedataFigures.Add(row);
        }

        row.Data = json;
        row.Group = FigureRecords.GroupOf(kind, record);

        if (before != json)
        {
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            var change = Change(GamedataRecordType.Figure, kind, key, before, json);

            change.RecordId = row.Id;
            dbCtx.GamedataChangeSets.Add(
                new GamedataChangeSetEntity
                {
                    Kind = GamedataChangeKind.Edit,
                    Summary = Truncate(
                        $"{(before is null ? "Added" : "Edited")} {Describe(kind, key)}",
                        GamedataChangeSetEntity.SUMMARY_MAX_LENGTH
                    ),
                    PlayerEntityId = player.Value,
                    Changes = [change],
                }
            );

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            Changed();
        }

        return (await EntriesAsync(dbCtx, [row], ct).ConfigureAwait(false))[0];
    }

    public async Task<ImmutableArray<FigurePaletteSnapshot>> GetPalettesAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .GamedataFigures.AsNoTracking()
            .Where(x => x.Kind == FigureRecordKind.Color || x.Kind == FigureRecordKind.SetType)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var entries = await EntriesAsync(
                dbCtx,
                [.. rows.Where(x => x.Kind == FigureRecordKind.Color)],
                ct
            )
            .ConfigureAwait(false);
        var palettes =
            new SortedDictionary<int, List<(FigureEntrySnapshot Entry, JsonObject Record)>>();
        var usedBy = new Dictionary<int, List<string>>();

        foreach (var entry in entries)
        {
            if (Read(entry.Data) is not { } record)
                continue;

            var id = record[FigureRecords.PALETTE]!.GetValue<int>();

            if (!palettes.TryGetValue(id, out var colors))
                palettes[id] = colors = [];

            colors.Add((entry, record));
        }

        // A palette a kind names that has no colours yet is still one to fill.
        foreach (var type in rows.Where(x => x.Kind == FigureRecordKind.SetType))
        {
            if (Read(type.Data)?[FigureRecords.PALETTE_ID]?.GetValue<int>() is not { } id)
                continue;

            palettes.TryAdd(id, []);

            if (!usedBy.TryGetValue(id, out var types))
                usedBy[id] = types = [];

            types.Add(type.Key);
        }

        return
        [
            .. palettes.Select(x => new FigurePaletteSnapshot
            {
                Id = x.Key,
                UsedBy = [.. (usedBy.GetValueOrDefault(x.Key) ?? []).Order(StringComparer.Ordinal)],
                Colors =
                [
                    .. x
                        .Value.OrderBy(c => c.Record[FigureRecords.INDEX]!.GetValue<int>())
                        .ThenBy(c => c.Record[FigureRecords.ID]!.GetValue<int>())
                        .Select(c => c.Entry),
                ],
            }),
        ];
    }

    public Task<int> SaveBatchAsync(
        FigureRecordKind kind,
        IReadOnlyList<string> save,
        IReadOnlyList<string> delete,
        string summary,
        PlayerId player,
        CancellationToken ct
    ) => writes.RunAsync(() => SaveBatchLockedAsync(kind, save, delete, summary, player, ct), ct);

    private async Task<int> SaveBatchLockedAsync(
        FigureRecordKind kind,
        IReadOnlyList<string> save,
        IReadOnlyList<string> delete,
        string summary,
        PlayerId player,
        CancellationToken ct
    )
    {
        // Every record is checked before anything is written.
        var records = new Dictionary<string, JsonObject>(StringComparer.Ordinal);

        foreach (var data in save)
        {
            JsonObject record;

            try
            {
                record = FigureRecords.Normalize(kind, FigureRecords.Parse(data));
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new ArgumentException("A figure record is a JSON object.", nameof(save), ex);
            }

            var key = FigureRecords.KeyOf(kind, record);

            if (key.Length > GamedataFigureEntity.KEY_MAX_LENGTH)
                throw new ArgumentException($"The key {key} is too long.", nameof(save));

            if (!records.TryAdd(key, record))
                throw new ArgumentException($"{Describe(kind, key)} is given twice.", nameof(save));
        }

        var removed = delete
            .Where(x => !records.ContainsKey(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var keys = records.Keys.Concat(removed).ToList();
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .GamedataFigures.Where(x => x.Kind == kind && keys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);
        var changes = new List<(GamedataChangeEntity Change, GamedataFigureEntity Row)>();

        foreach (var (key, record) in records)
        {
            var json = FigureRecords.Json(record);
            var group = FigureRecords.GroupOf(kind, record);

            if (rows.TryGetValue(key, out var row))
            {
                if (row.Data == json)
                    continue;

                changes.Add((Change(GamedataRecordType.Figure, kind, key, row.Data, json), row));
                row.Data = json;
                row.Group = group;
            }
            else
            {
                row = new GamedataFigureEntity
                {
                    Kind = kind,
                    Key = key,
                    Group = group,
                    Data = json,
                };
                dbCtx.GamedataFigures.Add(row);
                changes.Add((Change(GamedataRecordType.Figure, kind, key, null, json), row));
            }
        }

        foreach (var key in removed)
        {
            if (!rows.TryGetValue(key, out var row))
                continue;

            changes.Add((Change(GamedataRecordType.Figure, kind, key, row.Data, null), row));
            dbCtx.GamedataFigures.Remove(row);
        }

        if (changes.Count == 0)
            return 0;

        var tx = await dbCtx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var txScope = tx.ConfigureAwait(false);

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        foreach (var (change, row) in changes)
            change.RecordId = row.Id;

        dbCtx.GamedataChangeSets.Add(
            new GamedataChangeSetEntity
            {
                Kind = GamedataChangeKind.Edit,
                Summary = Truncate(
                    string.IsNullOrWhiteSpace(summary)
                        ? $"Edited {changes.Count} figure records"
                        : summary.Trim(),
                    GamedataChangeSetEntity.SUMMARY_MAX_LENGTH
                ),
                PlayerEntityId = player.Value,
                Changes = [.. changes.Select(x => x.Change)],
            }
        );

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        Changed();

        return changes.Count;
    }

    public Task<bool> DeleteAsync(
        FigureRecordKind kind,
        string key,
        PlayerId player,
        CancellationToken ct
    ) => writes.RunAsync(() => DeleteLockedAsync(kind, key, player, ct), ct);

    private async Task<bool> DeleteLockedAsync(
        FigureRecordKind kind,
        string key,
        PlayerId player,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .GamedataFigures.FirstOrDefaultAsync(x => x.Kind == kind && x.Key == key, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        var change = Change(GamedataRecordType.Figure, kind, key, row.Data, null);

        change.RecordId = row.Id;
        dbCtx.GamedataFigures.Remove(row);
        dbCtx.GamedataChangeSets.Add(
            new GamedataChangeSetEntity
            {
                Kind = GamedataChangeKind.Edit,
                Summary = Truncate(
                    $"Removed {Describe(kind, key)}",
                    GamedataChangeSetEntity.SUMMARY_MAX_LENGTH
                ),
                PlayerEntityId = player.Value,
                Changes = [change],
            }
        );

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        Changed();

        return true;
    }

    /// <summary>The kind a record is, by the fields only it has: what a change's record says of itself.</summary>
    internal static FigureRecordKind KindOf(JsonObject record) =>
        record.ContainsKey(FigureRecords.HEX) ? FigureRecordKind.Color
        : record.ContainsKey(FigureRecords.PALETTE_ID) ? FigureRecordKind.SetType
        : FigureRecordKind.Set;

    /// <summary>
    /// What an import does with one record, from Habbo's, the hotel's (null: it has none) and
    /// Habbo's last (null: never taken in). Each field is decided as a text is.
    /// </summary>
    internal static (
        FurnitureImportAction? Action,
        ImmutableArray<FurnitureFieldChange> Fields
    ) Decide(FigureRecordKind kind, JsonObject habbo, JsonObject? ours, JsonObject? previous)
    {
        var fields = FigureRecords.Fields(kind);

        if (ours is null)
        {
            if (previous is null)
                return (
                    FurnitureImportAction.Add,
                    [
                        .. fields.Select(x =>
                            Field(x, "null", FigureRecords.FieldJson(habbo, x), false)
                        ),
                    ]
                );

            // The hotel removed it: worth saying only when Habbo changed it since.
            return FigureRecords.Json(habbo) == FigureRecords.Json(previous)
                ? (null, [])
                : (
                    FurnitureImportAction.Keep,
                    [
                        .. fields.Select(x =>
                            Field(x, "null", FigureRecords.FieldJson(habbo, x), true)
                        ),
                    ]
                );
        }

        var changes = ImmutableArray.CreateBuilder<FurnitureFieldChange>();

        foreach (var field in fields)
        {
            var incoming = FigureRecords.FieldJson(habbo, field);
            var current = FigureRecords.FieldJson(ours, field);

            if (incoming == current)
                continue;

            // Never taken in, but the hotel has it: the hotel's own.
            var action = previous is null
                ? FurnitureImportAction.Keep
                : GamedataTextService.Decide(
                    incoming,
                    current,
                    FigureRecords.FieldJson(previous, field)
                );

            if (action is null)
                continue;

            changes.Add(Field(field, current, incoming, action == FurnitureImportAction.Keep));
        }

        return changes.Count == 0 ? (null, [])
            : changes.Any(x => !x.Kept) ? (FurnitureImportAction.Update, changes.ToImmutable())
            : (FurnitureImportAction.Keep, changes.ToImmutable());
    }

    /// <summary>After any change: the file the client loads is built again, and figures are checked against the new data.</summary>
    private void Changed()
    {
        files.Invalidate(GamedataFiles.FIGURE_DATA);
        figureData.Invalidate();
    }

    private static async Task<ImmutableArray<FigureEntrySnapshot>> EntriesAsync(
        TurboDbContext dbCtx,
        IReadOnlyCollection<GamedataFigureEntity> rows,
        CancellationToken ct
    )
    {
        var keys = rows.Select(x => x.Key).Distinct().ToList();
        var kinds = rows.Select(x => x.Kind).Distinct().ToList();
        var habbo = (
            await dbCtx
                .HabboFigures.AsNoTracking()
                .Where(x => kinds.Contains(x.Kind) && keys.Contains(x.Key))
                .ToListAsync(ct)
                .ConfigureAwait(false)
        ).ToDictionary(x => (x.Kind, x.Key));

        return
        [
            .. rows.Select(x =>
            {
                habbo.TryGetValue((x.Kind, x.Key), out var known);

                return new FigureEntrySnapshot
                {
                    Kind = x.Kind,
                    Key = x.Key,
                    Group = x.Group,
                    Data = x.Data,
                    FromHabbo = known is not null,
                    HabboData = known?.Data,
                };
            }),
        ];
    }

    private IQueryable<HabboFigureVersionEntity> LatestQuery(TurboDbContext dbCtx) =>
        dbCtx
            .HabboFigureVersions.Where(x => x.Domain == _config.HabboDomain)
            .OrderByDescending(x => x.Id);

    private Task<HabboFigureVersionEntity?> FindVersionAsync(
        TurboDbContext dbCtx,
        int? versionId,
        CancellationToken ct
    ) =>
        versionId is { } id
            ? dbCtx.HabboFigureVersions.FirstOrDefaultAsync(x => x.Id == id, ct)
            : LatestQuery(dbCtx).FirstOrDefaultAsync(ct);

    private static async Task<List<PlanItem>> PlanAsync(
        TurboDbContext dbCtx,
        HabboFigureVersionEntity version,
        bool tracked,
        CancellationToken ct
    )
    {
        var oursQuery = dbCtx.GamedataFigures.AsQueryable();
        var basesQuery = dbCtx.HabboFigures.AsQueryable();

        if (!tracked)
        {
            oursQuery = oursQuery.AsNoTracking();
            basesQuery = basesQuery.AsNoTracking();
        }

        var ours = (await oursQuery.ToListAsync(ct).ConfigureAwait(false)).ToDictionary(x =>
            (x.Kind, x.Key)
        );
        var bases = (await basesQuery.ToListAsync(ct).ConfigureAwait(false)).ToDictionary(x =>
            (x.Kind, x.Key)
        );
        var plan = new List<PlanItem>();

        // A key twice in Habbo's file: the client keeps the last, and so does the hotel.
        var habbo = FigureDataFile
            .Parse(GamedataBytes.Decompress(version.Content))
            .Select(x => (x.Kind, Key: FigureRecords.KeyOf(x.Kind, x.Record), x.Record))
            .Where(x => x.Key.Length <= GamedataFigureEntity.KEY_MAX_LENGTH)
            .GroupBy(x => (x.Kind, x.Key))
            .Select(x => x.Last());

        foreach (var (kind, key, record) in habbo)
        {
            ours.TryGetValue((kind, key), out var current);
            bases.TryGetValue((kind, key), out var known);

            var (action, fields) = Decide(
                kind,
                record,
                current is null ? null : Read(current.Data),
                known is null ? null : Read(known.Data)
            );

            plan.Add(new PlanItem(kind, key, record, current, known, action, fields));
        }

        return plan;
    }

    /// <summary>A stored record in its one shape; an unreadable one as none, so Habbo's replaces it.</summary>
    private static JsonObject? Read(string data)
    {
        try
        {
            var record = FigureRecords.Parse(data);

            return FigureRecords.Normalize(KindOf(record), record);
        }
        catch (Exception ex) when (ex is ArgumentException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static FurnitureFieldChange Field(
        string key,
        string current,
        string incoming,
        bool kept
    ) =>
        new()
        {
            Field = key,
            Current = current,
            Incoming = incoming,
            Kept = kept,
        };

    private static GamedataChangeEntity Change(
        GamedataRecordType type,
        PlanItem item,
        string? before,
        string? after
    ) => Change(type, item.Kind, item.Key, before, after);

    private static GamedataChangeEntity Change(
        GamedataRecordType type,
        FigureRecordKind kind,
        string key,
        string? before,
        string? after
    ) =>
        new()
        {
            RecordType = type,
            RecordId = 0,
            Label = Truncate(Describe(kind, key), GamedataChangeEntity.LABEL_MAX_LENGTH),
            Before = before,
            After = after,
        };

    internal static string Describe(FigureRecordKind kind, string key) =>
        kind switch
        {
            FigureRecordKind.Color => $"colour {key}",
            FigureRecordKind.SetType => $"clothing type {key}",
            _ => $"figure set {key}",
        };

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    private sealed record PlanItem(
        FigureRecordKind Kind,
        string Key,
        JsonObject Habbo,
        GamedataFigureEntity? Current,
        HabboFigureEntity? Base,
        FurnitureImportAction? Action,
        ImmutableArray<FurnitureFieldChange> Fields
    );
}
