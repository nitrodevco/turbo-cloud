using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Database.Entities.Gamedata;
using Turbo.Database.Extensions;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Habbo;
using Turbo.Gamedata.HotelView;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;
using static Turbo.Gamedata.Variables.GamedataVariableService;

namespace Turbo.Gamedata.Variables;

/// <summary>
/// Habbo's external variables as a source of updates (<see cref="IHabboVariableService"/>). Each of
/// Habbo's keys is compared three ways - Habbo's value now, Habbo's value when last taken in
/// (<c>habbo_variables</c>), and the hotel's (<c>gamedata_variables</c>):
/// <list type="bullet">
/// <item>the hotel has Habbo's value: nothing waits;</item>
/// <item>the hotel changed or removed it after taking Habbo's in, and Habbo hasn't changed it
/// since: the hotel's own, not offered;</item>
/// <item>otherwise it waits, added or updated.</item>
/// </list>
/// A key Habbo had when last taken in and has dropped waits to be removed, while the hotel has it.
/// Keys the hotel owns are never offered (<see cref="Ownership.IsOwned"/>). Habbo's file writes values as
/// bare text; the hotel keeps JSON, so a flag or a number stays one where the hotel's is
/// (<see cref="HotelViewService.JsonOf"/>).
/// </summary>
internal sealed class HabboVariableService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptionsMonitor<GamedataConfig> config,
    IGamedataFileService files,
    GamedataWriteLock writes,
    TimeProvider time,
    ILogger<HabboVariableService> logger
) : IHabboVariableService
{
    private const string NO_VERSION =
        "There is no such version of Habbo's variables. Check Habbo first.";

    public async Task<HabboVariableUpdatePreview?> PreviewAsync(
        int? versionId,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var version = await dbCtx
            .HabboVariableVersions.FindOrLatestAsync(versionId, config.CurrentValue.HabboDomain, ct)
            .ConfigureAwait(false);

        if (version is null)
            return null;

        var plan = await PlanAsync(dbCtx, version, tracked: false, ct).ConfigureAwait(false);

        return new HabboVariableUpdatePreview
        {
            Version = version.ToSnapshot(),
            Added = plan.Count(HabboVariableUpdateAction.Add),
            Updated = plan.Count(HabboVariableUpdateAction.Update),
            Removed = plan.Count(HabboVariableUpdateAction.Remove),
            Kept = plan.Kept,
            Protected = plan.Protected,
            Unchanged = plan.Unchanged,
            Items =
            [
                .. plan.Items.Select(x => new HabboVariableUpdateItem
                {
                    Key = x.Key,
                    Action = x.Action,
                    Current = x.Current?.Value,
                    Incoming = x.Incoming,
                    Habbo = x.Habbo,
                }),
            ],
        };
    }

    public Task<GamedataChangeSetSnapshot?> TakeAsync(
        int versionId,
        IReadOnlyCollection<string>? keys,
        PlayerId player,
        CancellationToken ct
    ) => writes.RunAsync(() => TakeLockedAsync(versionId, keys, player, ct), ct);

    private async Task<GamedataChangeSetSnapshot?> TakeLockedAsync(
        int versionId,
        IReadOnlyCollection<string>? keys,
        PlayerId player,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var version =
            await dbCtx
                .HabboVariableVersions.FindOrLatestAsync(
                    versionId,
                    config.CurrentValue.HabboDomain,
                    ct
                )
                .ConfigureAwait(false)
            ?? throw new ArgumentException(NO_VERSION, nameof(versionId));
        var plan = await PlanAsync(dbCtx, version, tracked: true, ct).ConfigureAwait(false);
        var picked = keys?.ToHashSet(StringComparer.Ordinal);
        var taken = plan.Items.Where(x => picked is null || picked.Contains(x.Key)).ToList();

        if (taken.Count == 0)
            return null;

        var tx = await dbCtx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var txScope = tx.ConfigureAwait(false);

        var variables = new List<(GamedataVariableEntity Row, VariableState? Before, bool Gone)>();
        var bases = new List<(HabboVariableEntity Row, BaseState? Before, bool Gone)>();

        foreach (var item in taken)
        {
            if (item.Action == HabboVariableUpdateAction.Remove)
            {
                variables.Add((item.Current!, State(item.Current!), true));
                dbCtx.GamedataVariables.Remove(item.Current!);

                if (item.Base is { } gone)
                {
                    bases.Add((gone, BaseState.Of(gone), true));
                    dbCtx.HabboVariables.Remove(gone);
                }

                continue;
            }

            if (item.Current is { } row)
            {
                variables.Add((row, State(row), false));
                row.Value = item.Incoming!;
            }
            else
            {
                row = new GamedataVariableEntity { Key = item.Key, Value = item.Incoming! };
                dbCtx.GamedataVariables.Add(row);
                variables.Add((row, null, false));
            }

            if (item.Base is { } known)
            {
                bases.Add((known, BaseState.Of(known), false));
                known.Value = item.Habbo!;
                known.HabboVariableVersionEntityId = version.Id;
            }
            else
            {
                var added = new HabboVariableEntity
                {
                    Key = item.Key,
                    Value = item.Habbo!,
                    HabboVariableVersionEntityId = version.Id,
                };

                dbCtx.HabboVariables.Add(added);
                bases.Add((added, null, false));
            }
        }

        version.ImportedAt = time.GetUtcNow().UtcDateTime;

        // The rows first, so the changes can name the ids of the ones just added.
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        var addedCount = taken.Count(x => x.Action == HabboVariableUpdateAction.Add);
        var updatedCount = taken.Count(x => x.Action == HabboVariableUpdateAction.Update);
        var removedCount = taken.Count(x => x.Action == HabboVariableUpdateAction.Remove);
        var changeSet = GamedataChangeSets.Create(
            GamedataChangeKind.Import,
            $"Habbo's variables ({version.VariableCount}): {addedCount} added, {updatedCount} updated, {removedCount} removed",
            player,
            [
                .. variables.Select(x =>
                    Change(x.Row.Key, x.Before, x.Gone ? null : State(x.Row), x.Row.Id)
                ),
                .. bases.Select(x =>
                    HabboChange(x.Row.Key, x.Before, x.Gone ? null : BaseState.Of(x.Row), x.Row.Id)
                ),
            ]
        );

        dbCtx.GamedataChangeSets.Add(changeSet);

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Player {PlayerId} took in Habbo's variables {VersionId}: {Added} added, {Updated} updated, {Removed} removed",
            player.Value,
            version.Id,
            addedCount,
            updatedCount,
            removedCount
        );

        files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);

        return changeSet.ToSnapshot(changeSet.Changes!.Count);
    }

    private async Task<Plan> PlanAsync(
        TurboDbContext dbCtx,
        HabboVariableVersionEntity version,
        bool tracked,
        CancellationToken ct
    )
    {
        var oursQuery = dbCtx.GamedataVariables.AsQueryable();
        var basesQuery = dbCtx.HabboVariables.AsQueryable();

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
        var habbo = HabboGamedataClient.ParseVariables(GamedataBytes.Decompress(version.Content));
        var owned = new Ownership(config.CurrentValue);
        var plan = new Plan();

        foreach (var (key, raw) in habbo.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            // A key too long for the column is not one the hotel can keep.
            if (key.Length > GamedataVariableEntity.KEY_MAX_LENGTH)
                continue;

            ours.TryGetValue(key, out var current);

            if (owned.IsOwned(key, raw, current))
            {
                plan.Protected++;

                continue;
            }

            var incoming = ExternalVariablesFile.Normalize(
                HotelViewService.JsonOf(raw, current?.Value)
            );

            if (current?.Value == incoming)
            {
                plan.Unchanged++;

                continue;
            }

            bases.TryGetValue(key, out var known);

            // The hotel changed or removed Habbo's value after taking it in: its own, until Habbo
            // changes it again.
            if (known?.Value == raw)
            {
                plan.Kept++;

                continue;
            }

            plan.Items.Add(
                new PlanItem(
                    key,
                    current is null
                        ? HabboVariableUpdateAction.Add
                        : HabboVariableUpdateAction.Update,
                    raw,
                    incoming,
                    current,
                    known
                )
            );
        }

        foreach (var known in bases.Values.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (habbo.ContainsKey(known.Key) || !ours.TryGetValue(known.Key, out var current))
                continue;

            if (owned.IsOwned(known.Key, null, current))
            {
                plan.Protected++;

                continue;
            }

            plan.Items.Add(
                new PlanItem(
                    known.Key,
                    HabboVariableUpdateAction.Remove,
                    null,
                    null,
                    current,
                    known
                )
            );
        }

        return plan;
    }

    private static GamedataChangeEntity HabboChange(
        string key,
        BaseState? before,
        BaseState? after,
        int id
    ) =>
        GamedataChangeSets.Change(
            GamedataRecordType.HabboVariable,
            key,
            before?.Record(key),
            after?.Record(key),
            id
        );

    /// <summary>
    /// The keys the hotel owns and never takes from Habbo: one that follows a setting or a file,
    /// one the hotel keeps itself (<see cref="GamedataVariableService.MANAGED"/>), the reception's
    /// (the Hotel view page keeps them), one <see cref="GamedataConfig.HabboVariableIgnore"/> names,
    /// and - with <see cref="GamedataConfig.HabboVariableSkipAddresses"/> - one whose value from
    /// Habbo is a web address.
    /// </summary>
    internal sealed class Ownership(GamedataConfig config)
    {
        private readonly Regex[] _ignored =
        [
            .. config
                .HabboVariableIgnore.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => new Regex(
                    $"^{Regex.Escape(x.Trim()).Replace("\\*", ".*", StringComparison.Ordinal)}$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
                )),
        ];

        public bool IsOwned(string key, string? habbo, GamedataVariableEntity? current) =>
            current?.SettingPath is not null
            || current?.LinkedFile is not null
            || IsManaged(key)
            || IsHotelView(key)
            || _ignored.Any(x => x.IsMatch(key))
            || (config.HabboVariableSkipAddresses && habbo is not null && IsAddress(habbo));

        private static bool IsAddress(string value) =>
            value.StartsWith("//", StringComparison.Ordinal)
            || value.Contains("://", StringComparison.Ordinal);
    }

    /// <summary>Habbo's value as last taken in, and the version it came from.</summary>
    private sealed record BaseState(string Value, int Version)
    {
        public static BaseState Of(HabboVariableEntity row) =>
            new(row.Value, row.HabboVariableVersionEntityId);

        public string Record(string key) =>
            new JsonObject
            {
                ["key"] = key,
                ["value"] = Value,
                ["version"] = Version,
            }.ToJsonString();
    }

    private sealed record PlanItem(
        string Key,
        HabboVariableUpdateAction Action,
        string? Habbo,
        string? Incoming,
        GamedataVariableEntity? Current,
        HabboVariableEntity? Base
    );

    private sealed class Plan
    {
        public List<PlanItem> Items { get; } = [];

        public int Kept { get; set; }

        public int Protected { get; set; }

        public int Unchanged { get; set; }

        public int Count(HabboVariableUpdateAction action) => Items.Count(x => x.Action == action);
    }
}
