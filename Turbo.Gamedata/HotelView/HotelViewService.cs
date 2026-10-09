using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
using Turbo.Gamedata.Variables;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Settings;
using Turbo.Primitives.Texts;

namespace Turbo.Gamedata.HotelView;

/// <summary>
/// The hotel view (<see cref="IHotelViewService"/>): its <c>landing.view.*</c> variables and the
/// texts its widgets show, saved together as one change set. Each change is recorded as
/// <see cref="GamedataVariableService"/> and <see cref="GamedataTextService"/> record theirs, so a
/// rollback restores both.
/// </summary>
internal sealed class HotelViewService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config,
    IGamedataFileService files,
    GamedataWriteLock writes,
    IServerSettings settings,
    IHotelTextProvider hotelTexts,
    ILogger<HotelViewService> logger
) : IHotelViewService
{
    /// <summary>What every key the client reads its reception from starts with.</summary>
    public const string PREFIX = "landing.view.";

    private readonly GamedataConfig _config = config.Value;

    public async Task<ImmutableArray<VariableEntrySnapshot>> GetVariablesAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .GamedataVariables.AsNoTracking()
            .Where(x => x.Key.StartsWith(PREFIX))
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

        return
        [
            .. rows.OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new VariableEntrySnapshot
                {
                    Key = x.Key,
                    Value = ExternalVariablesFile.Resolve(
                        x.Value,
                        x.SettingPath,
                        x.LinkedFile,
                        addresses,
                        settings
                    ),
                    Setting = x.SettingPath,
                    File = x.LinkedFile,
                }),
        ];
    }

    public async Task<ImmutableArray<TextEntrySnapshot>> GetTextsAsync(
        IReadOnlyCollection<string> keys,
        CancellationToken ct
    )
    {
        var wanted = keys.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToList();

        if (wanted.Count == 0)
            return [];

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var texts = await dbCtx
            .GamedataTexts.AsNoTracking()
            .Where(x => wanted.Contains(x.Key))
            .Select(x => new TextEntrySnapshot { Key = x.Key, Value = x.Value })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. texts.OrderBy(x => x.Key, StringComparer.Ordinal)];
    }

    public Task<GamedataChangeSetSnapshot?> SaveAsync(
        HotelViewEdit edit,
        PlayerId player,
        CancellationToken ct
    )
    {
        // Everything is checked before anything is written: a save is all or nothing.
        var variables = edit.Variables.ToDictionary(
            x => CheckVariableKey(x.Key),
            x => x.Value is null ? null : ExternalVariablesFile.Normalize(x.Value),
            StringComparer.Ordinal
        );
        var texts = edit.Texts.ToDictionary(
            x => GamedataTextService.CheckKey(x.Key),
            x => x.Value is null ? null : GamedataTextService.CheckValue(x.Value),
            StringComparer.Ordinal
        );

        return writes.RunAsync(() => SaveLockedAsync(variables, texts, player, ct), ct);
    }

    private async Task<GamedataChangeSetSnapshot?> SaveLockedAsync(
        Dictionary<string, string?> variables,
        Dictionary<string, string?> texts,
        PlayerId player,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var variableKeys = variables.Keys.ToList();
        var textKeys = texts.Keys.ToList();
        var variableRows = await dbCtx
            .GamedataVariables.Where(x => variableKeys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);
        var textRows = await dbCtx
            .GamedataTexts.Where(x => textKeys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);

        var variableChanges =
            new List<(
                string Key,
                GamedataVariableEntity Row,
                GamedataVariableService.VariableState? Before,
                GamedataVariableService.VariableState? After
            )>();
        var textChanges =
            new List<(string Key, GamedataTextEntity Row, string? Before, string? After)>();

        foreach (var (key, value) in variables.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            variableRows.TryGetValue(key, out var row);

            var before = row is null ? null : GamedataVariableService.State(row);

            if (value is null)
            {
                if (row is null)
                    continue;

                dbCtx.GamedataVariables.Remove(row);
                variableChanges.Add((key, row, before, null));

                continue;
            }

            var after = new GamedataVariableService.VariableState(value, null, null);

            if (before == after)
                continue;

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

            variableChanges.Add((key, row, before, after));
        }

        foreach (var (key, value) in texts.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            textRows.TryGetValue(key, out var row);

            if (row?.Value == value)
                continue;

            if (value is null)
            {
                dbCtx.GamedataTexts.Remove(row!);
                textChanges.Add((key, row!, row!.Value, null));

                continue;
            }

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

            textChanges.Add((key, row, before, value));
        }

        if (variableChanges.Count == 0 && textChanges.Count == 0)
            return null;

        var tx = await dbCtx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        await using var txScope = tx.ConfigureAwait(false);

        // The rows first, so the changes can name the ids of the ones just added.
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        var changeSet = new GamedataChangeSetEntity
        {
            Kind = GamedataChangeKind.Edit,
            Summary = Truncate(
                Summary(variableChanges.Count, textChanges.Count),
                GamedataChangeSetEntity.SUMMARY_MAX_LENGTH
            ),
            PlayerEntityId = player.Value,
            Changes =
            [
                .. variableChanges.Select(x =>
                    GamedataVariableService.Change(x.Key, x.Before, x.After, x.Row.Id)
                ),
                .. textChanges.Select(x =>
                    GamedataTextService.WithId(
                        GamedataTextService.Change(
                            GamedataRecordType.Text,
                            x.Key,
                            x.Before,
                            x.After
                        ),
                        x.Row.Id
                    )
                ),
            ],
        };

        dbCtx.GamedataChangeSets.Add(changeSet);

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Player {PlayerId} edited the hotel view: {Variables} variables, {Texts} texts",
            player.Value,
            variableChanges.Count,
            textChanges.Count
        );

        if (variableChanges.Count > 0)
            files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);

        if (textChanges.Count > 0)
        {
            files.Invalidate(GamedataFiles.EXTERNAL_TEXTS);
            hotelTexts.Invalidate();
        }

        return changeSet.ToSnapshot(changeSet.Changes!.Count);
    }

    /// <summary>A variable the hotel view may change: one the reception is read from.</summary>
    private static string CheckVariableKey(string key)
    {
        key = GamedataVariableService.CheckKey(key);

        if (!key.StartsWith(PREFIX, StringComparison.Ordinal) || key.Length == PREFIX.Length)
            throw new ArgumentException(
                $"The hotel view changes only variables under {PREFIX}; not {key}.",
                nameof(key)
            );

        return key;
    }

    private static string Summary(int variables, int texts) =>
        (variables, texts) switch
        {
            (_, 0) => $"Edited the hotel view: {Count(variables, "variable")}",
            (0, _) => $"Edited the hotel view: {Count(texts, "text")}",
            _ =>
                $"Edited the hotel view: {Count(variables, "variable")} and {Count(texts, "text")}",
        };

    private static string Count(int count, string what) =>
        count == 1 ? $"1 {what}" : $"{count} {what}s";

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
