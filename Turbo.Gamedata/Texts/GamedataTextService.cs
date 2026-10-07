using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Texts;

namespace Turbo.Gamedata.Texts;

/// <summary>
/// The hotel's external texts (<see cref="IGamedataTextService"/>). An import compares each of
/// Habbo's keys three ways - Habbo's new value, Habbo's value when last taken in
/// (<c>habbo_texts</c>), and the hotel's (<c>gamedata_texts</c>):
/// <list type="bullet">
/// <item>the hotel still has Habbo's old value: it takes the new one;</item>
/// <item>the hotel has no such text and Habbo had none before: it is added;</item>
/// <item>the hotel changed or removed it and Habbo changed it too: the hotel's stays, reported;</item>
/// <item>only the hotel changed or removed it: nothing to say.</item>
/// </list>
/// A key Habbo drops stays. A change records <c>{ key, value }</c> before and after.
/// </summary>
internal sealed class GamedataTextService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config,
    IGamedataFileService files,
    GamedataWriteLock writes,
    TimeProvider time,
    IHotelTextProvider hotelTexts,
    ILogger<GamedataTextService> logger
) : IGamedataTextService
{
    private const string KEY = "key";
    private const string VALUE = "value";

    private readonly GamedataConfig _config = config.Value;

    public async Task<HabboTextVersionSnapshot?> GetLatestVersionAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var latest = await LatestQuery(dbCtx)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return latest?.ToSnapshot();
    }

    public async Task<TextImportPreview?> PreviewImportAsync(int? versionId, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var version = await FindVersionAsync(dbCtx, versionId, ct).ConfigureAwait(false);

        if (version is null)
            return null;

        var plan = await PlanAsync(dbCtx, version, tracked: false, ct).ConfigureAwait(false);
        var touched = plan.Items.Where(x => x.Action is not null).ToList();

        return new TextImportPreview
        {
            Version = version.ToSnapshot(),
            Added = plan.Count(FurnitureImportAction.Add),
            Updated = plan.Count(FurnitureImportAction.Update),
            Kept = plan.Count(FurnitureImportAction.Keep),
            Unchanged = plan.Items.Count(x => x.Action is null),
            Items =
            [
                .. touched
                    .Take(_config.PreviewItemLimit)
                    .Select(x => new TextImportItem
                    {
                        Key = x.Key,
                        Action = x.Action!.Value,
                        Current = x.Current?.Value,
                        Incoming = x.Habbo,
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

        // Big imports (a first one is every text Habbo has) are written without EF looking for
        // changes on each of tens of thousands of rows at every step.
        dbCtx.ChangeTracker.AutoDetectChangesEnabled = false;

        var tx = await dbCtx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var txScope = tx.ConfigureAwait(false);

        foreach (var item in plan.Items)
        {
            if (item.Action == FurnitureImportAction.Add)
            {
                var row = new GamedataTextEntity { Key = item.Key, Value = item.Habbo };

                dbCtx.GamedataTexts.Add(row);
                changes.Add((Change(GamedataRecordType.Text, item.Key, null, item.Habbo), row));
                added++;
            }
            else if (item.Action == FurnitureImportAction.Update)
            {
                var row = item.Current!;
                var before = row.Value;

                row.Value = item.Habbo;
                dbCtx.Entry(row).State = EntityState.Modified;
                changes.Add((Change(GamedataRecordType.Text, item.Key, before, item.Habbo), row));
                updated++;
            }

            if (item.Base is { } known)
            {
                if (known.Value == item.Habbo)
                    continue;

                changes.Add(
                    (Change(GamedataRecordType.HabboText, item.Key, known.Value, item.Habbo), known)
                );
                known.Value = item.Habbo;
                known.HabboTextVersionEntityId = version.Id;
                dbCtx.Entry(known).State = EntityState.Modified;
            }
            else
            {
                var row = new HabboTextEntity
                {
                    Key = item.Key,
                    Value = item.Habbo,
                    HabboTextVersionEntityId = version.Id,
                };

                dbCtx.HabboTexts.Add(row);
                changes.Add(
                    (Change(GamedataRecordType.HabboText, item.Key, null, item.Habbo), row)
                );
            }
        }

        version.ImportedAt = time.GetUtcNow().UtcDateTime;
        dbCtx.Entry(version).State = EntityState.Modified;

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        GamedataChangeSetEntity? changeSet = null;

        if (changes.Count > 0)
        {
            changeSet = new GamedataChangeSetEntity
            {
                Kind = GamedataChangeKind.Import,
                Summary = Truncate(
                    $"Habbo's texts ({version.TextCount}): {added} added, {updated} updated, {plan.Count(FurnitureImportAction.Keep)} kept as the hotel has them",
                    GamedataChangeSetEntity.SUMMARY_MAX_LENGTH
                ),
                PlayerEntityId = player?.Value,
                Changes = [],
            };

            foreach (var (change, row) in changes)
            {
                change.RecordId = row switch
                {
                    GamedataTextEntity text => text.Id,
                    HabboTextEntity habbo => habbo.Id,
                    _ => 0,
                };
                changeSet.Changes.Add(change);
            }

            dbCtx.GamedataChangeSets.Add(changeSet);

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Imported Habbo's texts {VersionId}: {Added} added, {Updated} updated, {Kept} kept as the hotel has them",
            version.Id,
            added,
            updated,
            plan.Count(FurnitureImportAction.Keep)
        );

        files.Invalidate(GamedataFiles.EXTERNAL_TEXTS);
        hotelTexts.Invalidate();

        return changeSet?.ToSnapshot(changeSet.Changes!.Count);
    }

    public async Task<TextSearchResult> SearchAsync(string? query, int page, CancellationToken ct)
    {
        var size = Math.Max(1, _config.TextPageSize);
        var words = (query ?? string.Empty).Trim();
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var texts = dbCtx.GamedataTexts.AsNoTracking();

        if (words.Length > 0)
            texts = texts.Where(x => x.Key.Contains(words) || x.Value.Contains(words));

        var total = await texts.CountAsync(ct).ConfigureAwait(false);
        var rows = await texts
            .OrderBy(x => x.Key)
            .Skip(Math.Max(0, page) * size)
            .Take(size)
            .Select(x => new { x.Key, x.Value })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var keys = rows.Select(x => x.Key).ToList();
        var habbo = await dbCtx
            .HabboTexts.AsNoTracking()
            .Where(x => keys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, x => x.Value, ct)
            .ConfigureAwait(false);

        return new TextSearchResult
        {
            Items =
            [
                .. rows.Select(x => new TextEntrySnapshot
                {
                    Key = x.Key,
                    Value = x.Value,
                    Habbo = habbo.GetValueOrDefault(x.Key),
                }),
            ],
            Total = total,
            PageSize = size,
        };
    }

    public Task<TextEntrySnapshot> SaveAsync(
        string key,
        string value,
        PlayerId player,
        CancellationToken ct
    ) => writes.RunAsync(() => SaveLockedAsync(key, value, player, ct), ct);

    private async Task<TextEntrySnapshot> SaveLockedAsync(
        string key,
        string value,
        PlayerId player,
        CancellationToken ct
    )
    {
        key = CheckKey(key);
        value = CheckValue(value);

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .GamedataTexts.FirstOrDefaultAsync(x => x.Key == key, ct)
            .ConfigureAwait(false);
        var before = row?.Value;

        if (row is null)
        {
            row = new GamedataTextEntity { Key = key, Value = value };
            dbCtx.GamedataTexts.Add(row);
        }
        else
        {
            row.Value = value;
        }

        if (before != value)
        {
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            dbCtx.GamedataChangeSets.Add(
                new GamedataChangeSetEntity
                {
                    Kind = GamedataChangeKind.Edit,
                    Summary = Truncate(
                        before is null ? $"Added the text {key}" : $"Edited the text {key}",
                        GamedataChangeSetEntity.SUMMARY_MAX_LENGTH
                    ),
                    PlayerEntityId = player.Value,
                    Changes = [WithId(Change(GamedataRecordType.Text, key, before, value), row.Id)],
                }
            );

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            files.Invalidate(GamedataFiles.EXTERNAL_TEXTS);
            hotelTexts.Invalidate();
        }

        var habbo = await dbCtx
            .HabboTexts.AsNoTracking()
            .Where(x => x.Key == key)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return new TextEntrySnapshot
        {
            Key = key,
            Value = value,
            Habbo = habbo,
        };
    }

    public Task<bool> DeleteAsync(string key, PlayerId player, CancellationToken ct) =>
        writes.RunAsync(() => DeleteLockedAsync(key, player, ct), ct);

    private async Task<bool> DeleteLockedAsync(string key, PlayerId player, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .GamedataTexts.FirstOrDefaultAsync(x => x.Key == key, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        dbCtx.GamedataTexts.Remove(row);
        dbCtx.GamedataChangeSets.Add(
            new GamedataChangeSetEntity
            {
                Kind = GamedataChangeKind.Edit,
                Summary = Truncate(
                    $"Removed the text {key}",
                    GamedataChangeSetEntity.SUMMARY_MAX_LENGTH
                ),
                PlayerEntityId = player.Value,
                Changes = [WithId(Change(GamedataRecordType.Text, key, row.Value, null), row.Id)],
            }
        );

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        files.Invalidate(GamedataFiles.EXTERNAL_TEXTS);
        hotelTexts.Invalidate();

        return true;
    }

    /// <summary>A key the file can hold: one line, before the first <c>=</c>, not a comment.</summary>
    internal static string CheckKey(string key)
    {
        key = key.Trim();

        if (key.Length == 0)
            throw new ArgumentException("A text needs a key.", nameof(key));

        if (key.Length > GamedataTextEntity.KEY_MAX_LENGTH)
            throw new ArgumentException(
                $"A key can be at most {GamedataTextEntity.KEY_MAX_LENGTH} characters.",
                nameof(key)
            );

        if (key.Contains('=', StringComparison.Ordinal) || key.AsSpan().IndexOfAny('\r', '\n') >= 0)
            throw new ArgumentException("A key can't hold = or a line break.", nameof(key));

        if (key.StartsWith('#'))
            throw new ArgumentException(
                "A key can't start with #: the line would be a comment.",
                nameof(key)
            );

        return key;
    }

    /// <summary>A value the file can hold: one line, trimmed as the client trims it.</summary>
    internal static string CheckValue(string value)
    {
        if (value.AsSpan().IndexOfAny('\r', '\n') >= 0)
            throw new ArgumentException(
                "A text is one line: write a line break as \\n.",
                nameof(value)
            );

        return value.Trim();
    }

    private IQueryable<HabboTextVersionEntity> LatestQuery(TurboDbContext dbCtx) =>
        dbCtx
            .HabboTextVersions.Where(x => x.Domain == _config.HabboDomain)
            .OrderByDescending(x => x.Id);

    private Task<HabboTextVersionEntity?> FindVersionAsync(
        TurboDbContext dbCtx,
        int? versionId,
        CancellationToken ct
    ) =>
        versionId is { } id
            ? dbCtx.HabboTextVersions.FirstOrDefaultAsync(x => x.Id == id, ct)
            : LatestQuery(dbCtx).FirstOrDefaultAsync(ct);

    private static async Task<TextPlan> PlanAsync(
        TurboDbContext dbCtx,
        HabboTextVersionEntity version,
        bool tracked,
        CancellationToken ct
    )
    {
        var oursQuery = dbCtx.GamedataTexts.AsQueryable();
        var basesQuery = dbCtx.HabboTexts.AsQueryable();

        if (!tracked)
        {
            oursQuery = oursQuery.AsNoTracking();
            basesQuery = basesQuery.AsNoTracking();
        }

        var ours = await oursQuery
            .ToDictionaryAsync(x => x.Key, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);
        var bases = await basesQuery
            .ToDictionaryAsync(x => x.Key, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);
        var habbo = ExternalTextsFile.Parse(
            Encoding.UTF8.GetString(GamedataBytes.Decompress(version.Content))
        );
        var plan = new TextPlan();

        foreach (var (key, value) in habbo)
        {
            // A key too long for the column is not one the hotel can keep.
            if (key.Length > GamedataTextEntity.KEY_MAX_LENGTH)
                continue;

            ours.TryGetValue(key, out var current);
            bases.TryGetValue(key, out var known);
            plan.Items.Add(
                new TextPlanItem(
                    key,
                    value,
                    current,
                    known,
                    Decide(value, current?.Value, known?.Value)
                )
            );
        }

        return plan;
    }

    /// <summary>What an import does with one key: Habbo's value, the hotel's, Habbo's last.</summary>
    internal static FurnitureImportAction? Decide(string habbo, string? ours, string? previous)
    {
        if (ours == habbo)
            return null;

        if (previous is null)
            return ours is null ? FurnitureImportAction.Add : FurnitureImportAction.Keep;

        if (ours == previous)
            return FurnitureImportAction.Update;

        // The hotel changed or removed it: worth saying only when Habbo changed it too.
        return habbo == previous ? null : FurnitureImportAction.Keep;
    }

    private static GamedataChangeEntity Change(
        GamedataRecordType type,
        string key,
        string? before,
        string? after
    ) =>
        new()
        {
            RecordType = type,
            RecordId = 0,
            Label = Truncate(key, GamedataChangeEntity.LABEL_MAX_LENGTH),
            Before = before is null ? null : Record(key, before),
            After = after is null ? null : Record(key, after),
        };

    private static GamedataChangeEntity WithId(GamedataChangeEntity change, int id)
    {
        change.RecordId = id;

        return change;
    }

    private static string Record(string key, string value) =>
        new JsonObject { [KEY] = key, [VALUE] = value }.ToJsonString();

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    private sealed record TextPlanItem(
        string Key,
        string Habbo,
        GamedataTextEntity? Current,
        HabboTextEntity? Base,
        FurnitureImportAction? Action
    );

    private sealed class TextPlan
    {
        public List<TextPlanItem> Items { get; } = [];

        public int Count(FurnitureImportAction action) => Items.Count(x => x.Action == action);
    }
}
