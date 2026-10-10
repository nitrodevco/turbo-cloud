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
using Turbo.Primitives.Settings;

namespace Turbo.Gamedata.Variables;

/// <summary>
/// The client's external variables (<see cref="IGamedataVariableService"/>), kept in
/// <c>gamedata_variables</c> and built into the file by <see cref="Files.GamedataFileService"/>.
/// A variable may follow a server setting (<see cref="IServerSettings.GetValue"/>) or the address
/// of one of the hotel's gamedata files by hash (<see cref="ExternalVariablesFile.LINKABLE"/>);
/// setting a value of its own stops it. A change records <c>{ key, value, setting, file }</c>
/// before and after, the value as JSON text.
/// </summary>
internal sealed class GamedataVariableService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config,
    IGamedataFileService files,
    GamedataWriteLock writes,
    IServerSettings settings,
    ILogger<GamedataVariableService> logger
) : IGamedataVariableService
{
    private const string KEY = "key";
    private const string VALUE = "value";
    private const string SETTING = "setting";
    private const string FILE = "file";

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
            .Select(x => new
            {
                x.Key,
                x.Value,
                x.SettingPath,
                x.LinkedFile,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var addresses = await ExternalVariablesFile
            .AddressesAsync(files, _config.PublicUrl, ct)
            .ConfigureAwait(false);

        return new VariableSearchResult
        {
            Items =
            [
                .. rows.Select(x => Entry(x.Key, x.Value, x.SettingPath, x.LinkedFile, addresses)),
            ],
            Total = total,
            PageSize = size,
            LinkableFiles = [.. ExternalVariablesFile.LINKABLE],
            WritesAddresses = addresses.Root is not null,
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
        var before = row is null ? null : State(row);

        if (before == new VariableState(value, null, null))
            return new VariableEntrySnapshot { Key = key, Value = value };

        if (row is null)
        {
            row = new GamedataVariableEntity { Key = key, Value = value };
            dbCtx.GamedataVariables.Add(row);
        }
        else
        {
            // A value of its own: it follows nothing now.
            row.Value = value;
            row.SettingPath = null;
            row.LinkedFile = null;
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
                Changes = [Change(key, before, State(row), row.Id)],
            }
        );

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);

        return new VariableEntrySnapshot { Key = key, Value = value };
    }

    public Task<VariableEntrySnapshot> LinkAsync(
        string key,
        string? settingPath,
        string? file,
        PlayerId player,
        CancellationToken ct
    ) => writes.RunAsync(() => LinkLockedAsync(key, settingPath, file, player, ct), ct);

    private async Task<VariableEntrySnapshot> LinkLockedAsync(
        string key,
        string? settingPath,
        string? file,
        PlayerId player,
        CancellationToken ct
    )
    {
        key = CheckKey(key);

        var path = string.IsNullOrWhiteSpace(settingPath) ? null : CheckSetting(settingPath);
        var linked = string.IsNullOrWhiteSpace(file) ? null : CheckFile(file);

        if (path is not null && linked is not null)
            throw new ArgumentException(
                "A variable follows a setting or a file, not both.",
                nameof(file)
            );

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .GamedataVariables.FirstOrDefaultAsync(x => x.Key == key, ct)
            .ConfigureAwait(false);
        var before = row is null ? null : State(row);

        if (path is null && linked is null && row is null)
            throw new ArgumentException($"There is no variable {key}.", nameof(key));

        if (row is null)
        {
            row = new GamedataVariableEntity { Key = key, Value = "null" };
            dbCtx.GamedataVariables.Add(row);
        }

        if (path is not null)
        {
            // The setting's value now, kept should the setting ever go.
            row.Value = settings.GetValue(path) ?? row.Value;
            row.SettingPath = path;
            row.LinkedFile = null;
        }
        else if (linked is not null)
            Follow(row, linked, before);
        else
            Unlink(row);

        var after = State(row);

        if (before != after)
        {
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            dbCtx.GamedataChangeSets.Add(
                new GamedataChangeSetEntity
                {
                    Kind = GamedataChangeKind.Edit,
                    Summary = Truncate(
                        path is not null ? $"Linked the variable {key} to the setting {path}"
                            : linked is not null
                                ? $"Linked the variable {key} to the address of {linked}"
                            : $"Unlinked the variable {key}",
                        GamedataChangeSetEntity.SUMMARY_MAX_LENGTH
                    ),
                    PlayerEntityId = player.Value,
                    Changes = [Change(key, before, after, row.Id)],
                }
            );

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
        }

        var addresses = await ExternalVariablesFile
            .AddressesAsync(files, _config.PublicUrl, ct)
            .ConfigureAwait(false);

        return Entry(row.Key, row.Value, row.SettingPath, row.LinkedFile, addresses);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetFileKeysAsync(
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var linked = await dbCtx
            .GamedataVariables.AsNoTracking()
            .Where(x => x.LinkedFile != null)
            .Select(x => new { x.Key, x.LinkedFile })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return ExternalVariablesFile.LINKABLE.ToDictionary(
            file => file,
            IReadOnlyList<string> (file) =>
                [
                    .. linked
                        .Where(x => x.LinkedFile == file)
                        .Select(x => x.Key)
                        .Order(StringComparer.Ordinal),
                ],
            StringComparer.Ordinal
        );
    }

    public Task<VariableEntrySnapshot> SetFileKeyAsync(
        string file,
        string key,
        PlayerId player,
        CancellationToken ct
    ) => writes.RunAsync(() => SetFileKeyLockedAsync(file, key, player, ct), ct);

    private async Task<VariableEntrySnapshot> SetFileKeyLockedAsync(
        string file,
        string key,
        PlayerId player,
        CancellationToken ct
    )
    {
        key = CheckKey(key);
        file = CheckFile(file);

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .GamedataVariables.Where(x => x.Key == key || x.LinkedFile == file)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var row = rows.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal));
        var changes = new List<(GamedataVariableEntity Row, VariableState? Before)>();

        foreach (var other in rows.Where(x => x != row && x.LinkedFile == file))
        {
            changes.Add((other, State(other)));
            Unlink(other);
        }

        var before = row is null ? null : State(row);

        if (row is null)
        {
            row = new GamedataVariableEntity { Key = key, Value = "null" };
            dbCtx.GamedataVariables.Add(row);
        }

        Follow(row, file, before);

        if (before != State(row))
            changes.Add((row, before));

        if (changes.Count > 0)
        {
            var tx = await dbCtx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            await using var txScope = tx.ConfigureAwait(false);

            // The rows first, so the changes can name the id of one just added.
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            dbCtx.GamedataChangeSets.Add(
                new GamedataChangeSetEntity
                {
                    Kind = GamedataChangeKind.Edit,
                    Summary = Truncate(
                        $"Set the variable {key} to carry the address of {file}",
                        GamedataChangeSetEntity.SUMMARY_MAX_LENGTH
                    ),
                    PlayerEntityId = player.Value,
                    Changes =
                    [
                        .. changes.Select(x => Change(x.Row.Key, x.Before, State(x.Row), x.Row.Id)),
                    ],
                }
            );

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);

            files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
        }

        var addresses = await ExternalVariablesFile
            .AddressesAsync(files, _config.PublicUrl, ct)
            .ConfigureAwait(false);

        return Entry(row.Key, row.Value, row.SettingPath, row.LinkedFile, addresses);
    }

    /// <summary>
    /// A variable following the file's address. Its own value stays, written while there is no
    /// public address; a new one, or one that followed a setting, starts at the file's address that
    /// never changes.
    /// </summary>
    private void Follow(GamedataVariableEntity row, string file, VariableState? before)
    {
        if (before is null || before.Setting is not null)
            row.Value = ExternalVariablesFile.StableAddress(_config.PublicUrl, file);

        row.SettingPath = null;
        row.LinkedFile = file;
    }

    /// <summary>
    /// A variable following nothing, keeping what it was: the setting's value, or the file's address
    /// that never changes rather than one by hash that is pruned in time.
    /// </summary>
    private void Unlink(GamedataVariableEntity row)
    {
        row.Value =
            row.SettingPath is { } was ? settings.GetValue(was) ?? row.Value
            : row.LinkedFile is { } wasFile && !string.IsNullOrWhiteSpace(_config.PublicUrl)
                ? ExternalVariablesFile.StableAddress(_config.PublicUrl, wasFile)
            : row.Value;
        row.SettingPath = null;
        row.LinkedFile = null;
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
                Changes = [Change(key, State(row), null, row.Id)],
            }
        );

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);

        return true;
    }

    public async Task<VariableImportPreview> PreviewImportAsync(
        string json,
        bool removeMissing,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var plan = await PlanAsync(dbCtx, json, removeMissing, tracked: false, ct)
            .ConfigureAwait(false);
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
            Removed = [.. plan.Removed.Select(x => x.Key)],
        };
    }

    public Task<GamedataChangeSetSnapshot?> ImportAsync(
        string json,
        bool removeMissing,
        PlayerId player,
        CancellationToken ct
    ) => writes.RunAsync(() => ImportLockedAsync(json, removeMissing, player, ct), ct);

    private async Task<GamedataChangeSetSnapshot?> ImportLockedAsync(
        string json,
        bool removeMissing,
        PlayerId player,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var plan = await PlanAsync(dbCtx, json, removeMissing, tracked: true, ct)
            .ConfigureAwait(false);

        if (plan.Items.Count == 0 && plan.Removed.Count == 0)
            return null;

        var tx = await dbCtx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        await using var txScope = tx.ConfigureAwait(false);

        var changes = new List<(GamedataVariableEntity Row, VariableState? Before)>();

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
                changes.Add((row, State(row)));
                row.Value = item.Value;
            }
        }

        dbCtx.GamedataVariables.RemoveRange(plan.Removed);

        // The rows first, so the changes can name the ids of the ones just added.
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        var changeSet = new GamedataChangeSetEntity
        {
            Kind = GamedataChangeKind.Edit,
            Summary = Truncate(
                plan.Removed.Count == 0
                    ? $"Imported {plan.Items.Count} variables from a client config"
                    : $"Imported {plan.Items.Count} variables from a client config and removed {plan.Removed.Count} it lacks",
                GamedataChangeSetEntity.SUMMARY_MAX_LENGTH
            ),
            PlayerEntityId = player.Value,
            Changes =
            [
                .. changes.Select(x => Change(x.Row.Key, x.Before, State(x.Row), x.Row.Id)),
                .. plan.Removed.Select(x => Change(x.Key, State(x), null, x.Id)),
            ],
        };

        dbCtx.GamedataChangeSets.Add(changeSet);

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Player {PlayerId} imported {Count} external variables and removed {Removed}",
            player.Value,
            plan.Items.Count,
            plan.Removed.Count
        );

        files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);

        return changeSet.ToSnapshot(changeSet.Changes!.Count);
    }

    /// <summary>A key the hotel can keep: not empty, not too long.</summary>
    internal static string CheckKey(string key)
    {
        key = key.Trim();

        if (key.Length == 0)
            throw new ArgumentException("A variable needs a key.", nameof(key));

        if (key.Length > GamedataVariableEntity.KEY_MAX_LENGTH)
            throw new ArgumentException(
                $"A key can be at most {GamedataVariableEntity.KEY_MAX_LENGTH} characters.",
                nameof(key)
            );

        return key;
    }

    /// <summary>
    /// A setting a variable may follow: one the server has, and not a secret, which the public
    /// variables would give away. Throws <see cref="ArgumentException"/> otherwise.
    /// </summary>
    private string CheckSetting(string path)
    {
        var setting =
            settings.Get(path)
            ?? throw new ArgumentException($"There is no setting {path.Trim()}.", nameof(path));

        if (setting.Secret)
            throw new ArgumentException(
                $"{setting.Path} is a secret: the external variables are public.",
                nameof(path)
            );

        return setting.Path;
    }

    /// <summary>A file a variable may follow the address of. Throws <see cref="ArgumentException"/> otherwise.</summary>
    private static string CheckFile(string file)
    {
        file = file.Trim();

        if (!ExternalVariablesFile.IsLinkable(file))
            throw new ArgumentException(
                $"A variable can follow the address of {string.Join(", ", ExternalVariablesFile.LINKABLE)}; not {file}.",
                nameof(file)
            );

        return file;
    }

    /// <summary>A variable as the panel shows it: its value what the file writes for it now.</summary>
    private VariableEntrySnapshot Entry(
        string key,
        string value,
        string? setting,
        string? file,
        ExternalVariablesFile.Addresses addresses
    ) =>
        new()
        {
            Key = key,
            Value = ExternalVariablesFile.Resolve(value, setting, file, addresses, settings),
            Setting = setting,
            File = file,
        };

    internal static VariableState State(GamedataVariableEntity row) =>
        new(row.Value, row.SettingPath, row.LinkedFile);

    private async Task<VariablePlan> PlanAsync(
        TurboDbContext dbCtx,
        string json,
        bool removeMissing,
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

            ours.TryGetValue(key, out var current);

            // A variable that follows a setting or a file: the config can't set it.
            if (current?.SettingPath is not null || current?.LinkedFile is not null)
            {
                plan.Skipped.Add(key);

                continue;
            }

            if (current?.Value == value)
                plan.Unchanged++;
            else
                plan.Items.Add(new VariablePlanItem(key, value, current));
        }

        plan.Items.Reverse();
        plan.Skipped.Reverse();

        // What the config lacks, but not what follows a setting or a file: the hotel writes those.
        if (removeMissing)
            plan.Removed.AddRange(
                ours.Values.Where(x =>
                        !seen.Contains(x.Key) && x.SettingPath is null && x.LinkedFile is null
                    )
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
            );

        return plan;
    }

    internal static GamedataChangeEntity Change(
        string key,
        VariableState? before,
        VariableState? after,
        int id
    ) =>
        new()
        {
            RecordType = GamedataRecordType.Variable,
            RecordId = id,
            Label = Truncate(key, GamedataChangeEntity.LABEL_MAX_LENGTH),
            Before = before is null ? null : Record(key, before),
            After = after is null ? null : Record(key, after),
        };

    private static string Record(string key, VariableState state)
    {
        var record = new JsonObject { [KEY] = key, [VALUE] = state.Value };

        if (state.Setting is not null)
            record[SETTING] = state.Setting;

        if (state.File is not null)
            record[FILE] = state.File;

        return record.ToJsonString();
    }

    /// <summary>What a variable holds: its value, and the setting or file it follows, if any.</summary>
    internal sealed record VariableState(string Value, string? Setting, string? File);

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

        public List<GamedataVariableEntity> Removed { get; } = [];

        public int Unchanged { get; set; }
    }
}
