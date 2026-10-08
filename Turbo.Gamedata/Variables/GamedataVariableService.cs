using System;
using System.Collections.Generic;
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
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata.Variables;

/// <summary>
/// The client's external variables (<see cref="IGamedataVariableService"/>), kept in
/// <c>gamedata_variables</c> and built into the file by <see cref="Files.GamedataFileService"/>.
/// The variables the hotel writes itself (<see cref="ExternalVariablesFile.STAMPED"/>) can't be
/// set while it writes them. A change records <c>{ key, value }</c> before and after, the value
/// as JSON text.
/// </summary>
internal sealed class GamedataVariableService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config,
    IGamedataFileService files,
    GamedataWriteLock writes,
    ILogger<GamedataVariableService> logger
) : IGamedataVariableService
{
    private const string KEY = "key";
    private const string VALUE = "value";

    private readonly GamedataConfig _config = config.Value;

    public async Task<VariableSearchResult> SearchAsync(
        string? query,
        int page,
        CancellationToken ct
    )
    {
        var size = Math.Max(1, _config.TextPageSize);
        var words = (query ?? string.Empty).Trim();
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var variables = dbCtx.GamedataVariables.AsNoTracking();

        if (words.Length > 0)
            variables = variables.Where(x => x.Key.Contains(words) || x.Value.Contains(words));

        var total = await variables.CountAsync(ct).ConfigureAwait(false);
        var rows = await variables
            .OrderBy(x => x.Key)
            .Skip(Math.Max(0, page) * size)
            .Take(size)
            .Select(x => new { x.Key, x.Value })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var stamps = await ExternalVariablesFile
            .StampsAsync(files, _config.PublicUrl, ct)
            .ConfigureAwait(false);

        return new VariableSearchResult
        {
            Items =
            [
                .. rows.Select(x => new VariableEntrySnapshot { Key = x.Key, Value = x.Value }),
            ],
            Total = total,
            PageSize = size,
            Stamped =
            [
                .. stamps.Entries.Select(x => new VariableEntrySnapshot
                {
                    Key = x.Key,
                    Value = x.Value,
                }),
            ],
        };
    }

    public Task<VariableEntrySnapshot> SaveAsync(
        string key,
        string value,
        PlayerId player,
        CancellationToken ct
    ) => writes.RunAsync(() => SaveLockedAsync(key, value, player, ct), ct);

    private async Task<VariableEntrySnapshot> SaveLockedAsync(
        string key,
        string value,
        PlayerId player,
        CancellationToken ct
    )
    {
        key = CheckKey(key);
        value = ExternalVariablesFile.Normalize(value);

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .GamedataVariables.FirstOrDefaultAsync(x => x.Key == key, ct)
            .ConfigureAwait(false);
        var before = row?.Value;

        if (before == value)
            return new VariableEntrySnapshot { Key = key, Value = value };

        if (row is null)
        {
            row = new GamedataVariableEntity { Key = key, Value = value };
            dbCtx.GamedataVariables.Add(row);
        }
        else
        {
            row.Value = value;
        }

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        dbCtx.GamedataChangeSets.Add(
            new GamedataChangeSetEntity
            {
                Kind = GamedataChangeKind.Edit,
                Summary = Truncate(
                    before is null ? $"Added the variable {key}" : $"Edited the variable {key}",
                    GamedataChangeSetEntity.SUMMARY_MAX_LENGTH
                ),
                PlayerEntityId = player.Value,
                Changes = [Change(key, before, value, row.Id)],
            }
        );

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);

        return new VariableEntrySnapshot { Key = key, Value = value };
    }

    public Task<bool> DeleteAsync(string key, PlayerId player, CancellationToken ct) =>
        writes.RunAsync(() => DeleteLockedAsync(key, player, ct), ct);

    private async Task<bool> DeleteLockedAsync(string key, PlayerId player, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .GamedataVariables.FirstOrDefaultAsync(x => x.Key == key, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        dbCtx.GamedataVariables.Remove(row);
        dbCtx.GamedataChangeSets.Add(
            new GamedataChangeSetEntity
            {
                Kind = GamedataChangeKind.Edit,
                Summary = Truncate(
                    $"Removed the variable {key}",
                    GamedataChangeSetEntity.SUMMARY_MAX_LENGTH
                ),
                PlayerEntityId = player.Value,
                Changes = [Change(key, row.Value, null, row.Id)],
            }
        );

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);

        return true;
    }

    public async Task<VariableImportPreview> PreviewImportAsync(string json, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var plan = await PlanAsync(dbCtx, json, tracked: false, ct).ConfigureAwait(false);
        var limit = Math.Max(0, _config.PreviewItemLimit);

        return new VariableImportPreview
        {
            Added = plan.Items.Count(x => x.Current is null),
            Updated = plan.Items.Count(x => x.Current is not null),
            Unchanged = plan.Unchanged,
            Skipped = [.. plan.Skipped],
            Items =
            [
                .. plan
                    .Items.Take(limit)
                    .Select(x => new VariableImportItem
                    {
                        Key = x.Key,
                        Action = x.Current is null
                            ? FurnitureImportAction.Add
                            : FurnitureImportAction.Update,
                        Current = x.Current?.Value,
                        Incoming = x.Value,
                    }),
            ],
            Truncated = plan.Items.Count > limit,
        };
    }

    public Task<GamedataChangeSetSnapshot?> ImportAsync(
        string json,
        PlayerId player,
        CancellationToken ct
    ) => writes.RunAsync(() => ImportLockedAsync(json, player, ct), ct);

    private async Task<GamedataChangeSetSnapshot?> ImportLockedAsync(
        string json,
        PlayerId player,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var plan = await PlanAsync(dbCtx, json, tracked: true, ct).ConfigureAwait(false);

        if (plan.Items.Count == 0)
            return null;

        var tx = await dbCtx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        await using var txScope = tx.ConfigureAwait(false);

        var changes = new List<(GamedataVariableEntity Row, string? Before)>();

        foreach (var item in plan.Items)
        {
            var row = item.Current;

            if (row is null)
            {
                row = new GamedataVariableEntity { Key = item.Key, Value = item.Value };
                dbCtx.GamedataVariables.Add(row);
                changes.Add((row, null));
            }
            else
            {
                changes.Add((row, row.Value));
                row.Value = item.Value;
            }
        }

        // The rows first, so the changes can name the ids of the ones just added.
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        var changeSet = new GamedataChangeSetEntity
        {
            Kind = GamedataChangeKind.Edit,
            Summary = Truncate(
                $"Imported {plan.Items.Count} variables from a client config",
                GamedataChangeSetEntity.SUMMARY_MAX_LENGTH
            ),
            PlayerEntityId = player.Value,
            Changes = [.. changes.Select(x => Change(x.Row.Key, x.Before, x.Row.Value, x.Row.Id))],
        };

        dbCtx.GamedataChangeSets.Add(changeSet);

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Player {PlayerId} imported {Count} external variables",
            player.Value,
            plan.Items.Count
        );

        files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);

        return changeSet.ToSnapshot(changeSet.Changes!.Count);
    }

    /// <summary>
    /// A key the hotel can keep and staff may set: not empty, not too long, not one of the
    /// addresses the hotel writes itself while it writes them.
    /// </summary>
    private string CheckKey(string key)
    {
        key = key.Trim();

        if (key.Length == 0)
            throw new ArgumentException("A variable needs a key.", nameof(key));

        if (key.Length > GamedataVariableEntity.KEY_MAX_LENGTH)
            throw new ArgumentException(
                $"A key can be at most {GamedataVariableEntity.KEY_MAX_LENGTH} characters.",
                nameof(key)
            );

        if (IsWrittenByHotel(key))
            throw new ArgumentException(
                $"{key} is the address of one of the hotel's gamedata files; the hotel writes it itself.",
                nameof(key)
            );

        return key;
    }

    private bool IsWrittenByHotel(string key) =>
        !string.IsNullOrWhiteSpace(_config.PublicUrl) && ExternalVariablesFile.IsStamped(key);

    private async Task<VariablePlan> PlanAsync(
        TurboDbContext dbCtx,
        string json,
        bool tracked,
        CancellationToken ct
    )
    {
        var incoming = ExternalVariablesFile.Parse(json);
        var query = dbCtx.GamedataVariables.AsQueryable();

        if (!tracked)
            query = query.AsNoTracking();

        var ours = await query
            .ToDictionaryAsync(x => x.Key, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);
        var plan = new VariablePlan();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // A key given twice keeps its last value, as the client's own parse does.
        foreach (var (key, value) in Enumerable.Reverse(incoming))
        {
            if (!seen.Add(key))
                continue;

            if (key.Length == 0 || key.Length > GamedataVariableEntity.KEY_MAX_LENGTH)
            {
                plan.Skipped.Add(key);

                continue;
            }

            if (IsWrittenByHotel(key))
            {
                plan.Skipped.Add(key);

                continue;
            }

            ours.TryGetValue(key, out var current);

            if (current?.Value == value)
                plan.Unchanged++;
            else
                plan.Items.Add(new VariablePlanItem(key, value, current));
        }

        plan.Items.Reverse();
        plan.Skipped.Reverse();

        return plan;
    }

    private static GamedataChangeEntity Change(string key, string? before, string? after, int id) =>
        new()
        {
            RecordType = GamedataRecordType.Variable,
            RecordId = id,
            Label = Truncate(key, GamedataChangeEntity.LABEL_MAX_LENGTH),
            Before = before is null ? null : Record(key, before),
            After = after is null ? null : Record(key, after),
        };

    private static string Record(string key, string value) =>
        new JsonObject { [KEY] = key, [VALUE] = value }.ToJsonString();

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    private sealed record VariablePlanItem(
        string Key,
        string Value,
        GamedataVariableEntity? Current
    );

    private sealed class VariablePlan
    {
        public List<VariablePlanItem> Items { get; } = [];

        public List<string> Skipped { get; } = [];

        public int Unchanged { get; set; }
    }
}
