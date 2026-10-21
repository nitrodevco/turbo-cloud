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
)
    : HabboMirroredService<
        HabboFigureVersionEntity,
        GamedataFigureEntity,
        HabboFigureEntity,
        GamedataFigureService.PlanItem,
        FigureImportPreview
    >(dbCtxFactory, config, writes, time, logger),
        IGamedataFigureService
{
    protected override string What => "figure data";

    protected override FigureImportPreview Preview(
        HabboFigureVersionEntity version,
        List<PlanItem> plan,
        IEnumerable<PlanItem> shown,
        bool truncated
    ) =>
        new()
        {
            Version = version.ToSnapshot(),
            Added = Count(plan, FurnitureImportAction.Add),
            Updated = Count(plan, FurnitureImportAction.Update),
            Kept = Count(plan, FurnitureImportAction.Keep),
            Unchanged = Count(plan, null),
            Items =
            [
                .. shown.Select(x => new FigureImportItem
                {
                    Kind = x.Kind,
                    Key = x.Key,
                    Action = x.Action!.Value,
                    Fields = x.Fields,
                }),
            ],
            Truncated = truncated,
        };

    protected override (GamedataFigureEntity Row, GamedataChangeEntity Change) AddOurs(
        PlanItem item
    )
    {
        var habbo = FigureRecords.Json(item.Habbo);

        return (
            new GamedataFigureEntity
            {
                Kind = item.Kind,
                Key = item.Key,
                Group = FigureRecords.GroupOf(item.Kind, item.Habbo),
                Data = habbo,
            },
            Change(GamedataRecordType.Figure, item.Kind, item.Key, null, habbo)
        );
    }

    protected override GamedataChangeEntity UpdateOurs(PlanItem item, GamedataFigureEntity row)
    {
        var before = row.Data;
        var record = FigureRecords.Parse(row.Data);

        foreach (var field in item.Fields.Where(x => !x.Kept))
            record[field.Field] = item.Habbo[field.Field]?.DeepClone();

        record = FigureRecords.Normalize(item.Kind, record);
        row.Data = FigureRecords.Json(record);
        row.Group = FigureRecords.GroupOf(item.Kind, record);

        return Change(GamedataRecordType.Figure, item.Kind, item.Key, before, row.Data);
    }

    protected override GamedataChangeEntity? TakeHabbo(
        PlanItem item,
        HabboFigureEntity known,
        int versionId
    )
    {
        var habbo = FigureRecords.Json(item.Habbo);

        if (known.Data == habbo)
            return null;

        var change = Change(GamedataRecordType.HabboFigure, item.Kind, item.Key, known.Data, habbo);

        known.Data = habbo;
        known.HabboFigureVersionEntityId = versionId;

        return change;
    }

    protected override (HabboFigureEntity Row, GamedataChangeEntity Change) AddHabbo(
        PlanItem item,
        int versionId
    )
    {
        var habbo = FigureRecords.Json(item.Habbo);

        return (
            new HabboFigureEntity
            {
                Kind = item.Kind,
                Key = item.Key,
                Data = habbo,
                HabboFigureVersionEntityId = versionId,
            },
            Change(GamedataRecordType.HabboFigure, item.Kind, item.Key, null, habbo)
        );
    }

    protected override string ImportSummary(
        HabboFigureVersionEntity version,
        int added,
        int updated,
        int kept
    ) =>
        $"Habbo's figure data ({version.SetCount} pieces, {version.ColorCount} colours): {added} added, {updated} updated, {kept} kept as the hotel has them";

    /// <summary>After any change: the file the client loads is built again, and figures are checked against the new data.</summary>
    protected override void Changed()
    {
        files.Invalidate(GamedataFiles.FIGURE_DATA);
        figureData.Invalidate();
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
        var size = Math.Max(1, Config.TextPageSize);
        var words = (query ?? string.Empty).Trim();
        var dbCtx = await DbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
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
        var dbCtx = await DbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
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
    ) => Writes.RunAsync(() => SaveLockedAsync(kind, data, player, ct), ct);

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
        var dbCtx = await DbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
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

            dbCtx.GamedataChangeSets.Add(
                GamedataChangeSets.Create(
                    GamedataChangeKind.Edit,
                    $"{(before is null ? "Added" : "Edited")} {Describe(kind, key)}",
                    player,
                    [Change(GamedataRecordType.Figure, kind, key, before, json, row.Id)]
                )
            );

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            Changed();
        }

        return (await EntriesAsync(dbCtx, [row], ct).ConfigureAwait(false))[0];
    }

    public async Task<ImmutableArray<FigurePaletteSnapshot>> GetPalettesAsync(CancellationToken ct)
    {
        var dbCtx = await DbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
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
    ) => Writes.RunAsync(() => SaveBatchLockedAsync(kind, save, delete, summary, player, ct), ct);

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
        var dbCtx = await DbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
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
            GamedataChangeSets.Create(
                GamedataChangeKind.Edit,
                string.IsNullOrWhiteSpace(summary)
                    ? $"Edited {changes.Count} figure records"
                    : summary.Trim(),
                player,
                [.. changes.Select(x => x.Change)]
            )
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
    ) =>
        RemoveAsync(
            x => x.Kind == kind && x.Key == key,
            row => Change(GamedataRecordType.Figure, kind, key, row.Data, null),
            $"Removed {Describe(kind, key)}",
            player,
            ct
        );

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

    protected override async Task<List<PlanItem>> PlanAsync(
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

    /// <param name="recordId">The record's row; 0 for one not saved yet.</param>
    private static GamedataChangeEntity Change(
        GamedataRecordType type,
        FigureRecordKind kind,
        string key,
        string? before,
        string? after,
        int recordId = 0
    ) => GamedataChangeSets.Change(type, Describe(kind, key), before, after, recordId);

    internal static string Describe(FigureRecordKind kind, string key) =>
        kind switch
        {
            FigureRecordKind.Color => $"colour {key}",
            FigureRecordKind.SetType => $"clothing type {key}",
            _ => $"figure set {key}",
        };

    internal sealed record PlanItem(
        FigureRecordKind Kind,
        string Key,
        JsonObject Habbo,
        GamedataFigureEntity? Current,
        HabboFigureEntity? Base,
        FurnitureImportAction? Action,
        ImmutableArray<FurnitureFieldChange> Fields
    ) : IHabboImportItem<GamedataFigureEntity, HabboFigureEntity>;
}
