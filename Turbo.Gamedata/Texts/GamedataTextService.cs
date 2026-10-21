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
)
    : HabboMirroredService<
        HabboTextVersionEntity,
        GamedataTextEntity,
        HabboTextEntity,
        GamedataTextService.TextPlanItem,
        TextImportPreview
    >(dbCtxFactory, config, writes, time, logger),
        IGamedataTextService
{
    private const string KEY = "key";
    private const string VALUE = "value";

    protected override string What => "texts";

    protected override TextImportPreview Preview(
        HabboTextVersionEntity version,
        List<TextPlanItem> plan,
        IEnumerable<TextPlanItem> shown,
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
                .. shown.Select(x => new TextImportItem
                {
                    Key = x.Key,
                    Action = x.Action!.Value,
                    Current = x.Current?.Value,
                    Incoming = x.Habbo,
                }),
            ],
            Truncated = truncated,
        };

    protected override (GamedataTextEntity Row, GamedataChangeEntity Change) AddOurs(
        TextPlanItem item
    ) =>
        (
            new GamedataTextEntity { Key = item.Key, Value = item.Habbo },
            Change(GamedataRecordType.Text, item.Key, null, item.Habbo)
        );

    protected override GamedataChangeEntity UpdateOurs(TextPlanItem item, GamedataTextEntity row)
    {
        var before = row.Value;

        row.Value = item.Habbo;

        return Change(GamedataRecordType.Text, item.Key, before, item.Habbo);
    }

    protected override GamedataChangeEntity? TakeHabbo(
        TextPlanItem item,
        HabboTextEntity known,
        int versionId
    )
    {
        if (known.Value == item.Habbo)
            return null;

        var change = Change(GamedataRecordType.HabboText, item.Key, known.Value, item.Habbo);

        known.Value = item.Habbo;
        known.HabboTextVersionEntityId = versionId;

        return change;
    }

    protected override (HabboTextEntity Row, GamedataChangeEntity Change) AddHabbo(
        TextPlanItem item,
        int versionId
    ) =>
        (
            new HabboTextEntity
            {
                Key = item.Key,
                Value = item.Habbo,
                HabboTextVersionEntityId = versionId,
            },
            Change(GamedataRecordType.HabboText, item.Key, null, item.Habbo)
        );

    protected override string ImportSummary(
        HabboTextVersionEntity version,
        int added,
        int updated,
        int kept
    ) =>
        $"Habbo's texts ({version.TextCount}): {added} added, {updated} updated, {kept} kept as the hotel has them";

    protected override void Changed()
    {
        files.Invalidate(GamedataFiles.EXTERNAL_TEXTS);
        hotelTexts.Invalidate();
    }

    public async Task<TextSearchResult> SearchAsync(string? query, int page, CancellationToken ct)
    {
        var size = Math.Max(1, Config.TextPageSize);
        var words = (query ?? string.Empty).Trim();
        var dbCtx = await DbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
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
    ) => Writes.RunAsync(() => SaveLockedAsync(key, value, player, ct), ct);

    private async Task<TextEntrySnapshot> SaveLockedAsync(
        string key,
        string value,
        PlayerId player,
        CancellationToken ct
    )
    {
        key = CheckKey(key);
        value = CheckValue(value);

        var dbCtx = await DbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
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
                GamedataChangeSets.Create(
                    GamedataChangeKind.Edit,
                    before is null ? $"Added the text {key}" : $"Edited the text {key}",
                    player,
                    [Change(GamedataRecordType.Text, key, before, value, row.Id)]
                )
            );

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            Changed();
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
        RemoveAsync(
            x => x.Key == key,
            row => Change(GamedataRecordType.Text, key, row.Value, null),
            $"Removed the text {key}",
            player,
            ct
        );

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

    protected override async Task<List<TextPlanItem>> PlanAsync(
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
        var plan = new List<TextPlanItem>();

        foreach (var (key, value) in habbo)
        {
            // A key too long for the column is not one the hotel can keep.
            if (key.Length > GamedataTextEntity.KEY_MAX_LENGTH)
                continue;

            ours.TryGetValue(key, out var current);
            bases.TryGetValue(key, out var known);
            plan.Add(
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

    /// <param name="recordId">The text's row; 0 for one not saved yet.</param>
    internal static GamedataChangeEntity Change(
        GamedataRecordType type,
        string key,
        string? before,
        string? after,
        int recordId = 0
    ) =>
        GamedataChangeSets.Change(
            type,
            key,
            before is null ? null : Record(key, before),
            after is null ? null : Record(key, after),
            recordId
        );

    private static string Record(string key, string value) =>
        new JsonObject { [KEY] = key, [VALUE] = value }.ToJsonString();

    internal sealed record TextPlanItem(
        string Key,
        string Habbo,
        GamedataTextEntity? Current,
        HabboTextEntity? Base,
        FurnitureImportAction? Action
    ) : IHabboImportItem<GamedataTextEntity, HabboTextEntity>;
}
