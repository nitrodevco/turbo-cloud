using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Gamedata;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;

namespace Turbo.Gamedata.Plugins;

/// <summary>
/// A plugin's own external variables and texts (<see cref="IPluginGamedataSeeds"/>): given to the
/// hotel as the plugin loads when the hotel has none of the key, through the same services the
/// panel's edits and imports go through, each load one change set by nobody. What the plugin gave
/// is kept (<see cref="GamedataPluginValueEntity"/>): a key still holding it is the plugin's own,
/// follows the plugin's value when a new version gives another (or goes when it gives none), and
/// is sent to clients only while the plugin is loaded (<see cref="Loaded"/>). A key the hotel has of
/// its own is never touched; one already holding the plugin's value when the plugin first brings it
/// (a hotel that ran the plugin before its values were kept) becomes the plugin's. A key the hotel
/// changed since is the hotel's. On an uninstall with its data (<see cref="IPluginDataOwner"/>) the
/// ones still as the plugin gave them go; the hotel's changes stay.
/// </summary>
internal sealed class PluginGamedataSeeds(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGamedataVariableService variables,
    IGamedataTextService texts,
    IGamedataFileService files,
    LoadedPluginGamedata loaded,
    ILogger<PluginGamedataSeeds> logger
) : IPluginGamedataSeeds, IPluginDataOwner
{
    /// <summary>The folder in a plugin's own that holds what it brings of the client's gamedata.</summary>
    public const string FOLDER = "gamedata";

    /// <summary>Its external variables: a JSON object, as a client config is.</summary>
    public const string VARIABLES_FILE = "external_variables.json";

    /// <summary>Its external texts: <c>key=value</c> lines, as Habbo's file is.</summary>
    public const string TEXTS_FILE = "external_texts.txt";

    public async Task SeedAsync(string pluginKey, string folder, CancellationToken ct)
    {
        var (variableSeed, textSeed) = await ReadAsync(pluginKey, folder, ct).ConfigureAwait(false);

        await FollowAsync(pluginKey, Variables, variableSeed, ct).ConfigureAwait(false);
        await FollowAsync(pluginKey, Texts, textSeed, ct).ConfigureAwait(false);
    }

    public IDisposable Loaded(string pluginKey) => loaded.Add(pluginKey, Invalidate);

    public async Task<ImmutableArray<PluginDataItem>> FindAsync(
        PluginDataScope plugin,
        CancellationToken ct
    )
    {
        var (variableSeed, textSeed) = plugin.Folder is null
            ? (new Dictionary<string, string>(), new Dictionary<string, string>())
            : await ReadAsync(plugin.Key, plugin.Folder, ct).ConfigureAwait(false);
        var items = ImmutableArray.CreateBuilder<PluginDataItem>();

        items.AddRange(
            await UnchangedAsync(plugin.Key, Variables, variableSeed, ct).ConfigureAwait(false)
        );
        items.AddRange(await UnchangedAsync(plugin.Key, Texts, textSeed, ct).ConfigureAwait(false));

        return items.ToImmutable();
    }

    public async Task RemoveAsync(
        PluginDataScope plugin,
        ImmutableArray<PluginDataItem> found,
        PlayerId actor,
        CancellationToken ct
    )
    {
        var variableSeed = Values(found, PluginDataKind.Variable);
        var textSeed = Values(found, PluginDataKind.Text);

        if (variableSeed.Count > 0)
            await variables
                .RemoveUnchangedAsync(
                    variableSeed,
                    $"The plugin {plugin.Key} was uninstalled with its variables",
                    actor,
                    ct
                )
                .ConfigureAwait(false);

        if (textSeed.Count > 0)
            await texts
                .RemoveUnchangedAsync(
                    textSeed,
                    $"The plugin {plugin.Key} was uninstalled with its texts",
                    actor,
                    ct
                )
                .ConfigureAwait(false);

        // What it changed is the hotel's now.
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        await dbCtx
            .GamedataPluginValues.Where(x => x.PluginKey == plugin.Key)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        Invalidate();
    }

    /// <summary>
    /// Gives the hotel what the plugin brings of one kind that it has none of, gives the keys the
    /// plugin owns and still hold its old value its new one (or takes them away when it brings
    /// them no more), then keeps what it gave. A key another plugin owns is left to that one.
    /// </summary>
    private async Task FollowAsync(
        string pluginKey,
        Kind kind,
        IReadOnlyDictionary<string, string> seed,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var owned = await dbCtx
            .GamedataPluginValues.Where(x => x.RecordType == kind.Type)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var mine = owned
            .Where(x => IsPlugin(x.PluginKey, pluginKey))
            .ToDictionary(x => x.Key, StringComparer.Ordinal);
        var others = owned
            .Where(x => !IsPlugin(x.PluginKey, pluginKey))
            .Select(x => x.Key)
            .ToHashSet(StringComparer.Ordinal);
        var wanted = seed.Where(x => !others.Contains(x.Key))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        if (wanted.Count == 0 && mine.Count == 0)
            return;

        if (wanted.Count > 0)
        {
            var added = await kind.Seed(wanted, $"The plugin {pluginKey} added its {kind.Noun}", ct)
                .ConfigureAwait(false);

            if (added is not null)
                logger.LogInformation(
                    "The plugin {Key} added {Count} external {Kind}",
                    pluginKey,
                    added.ChangeCount,
                    kind.Noun
                );
        }

        if (mine.Count > 0)
        {
            var changes = mine
                .Values.Where(x => !wanted.TryGetValue(x.Key, out var value) || value != x.Value)
                .ToDictionary(
                    x => x.Key,
                    x => new GamedataValueChange(
                        x.Value,
                        wanted.TryGetValue(x.Key, out var value) ? value : null
                    ),
                    StringComparer.Ordinal
                );

            if (changes.Count > 0)
            {
                var updated = await kind.Replace(
                        changes,
                        $"The plugin {pluginKey} updated its {kind.Noun}",
                        ct
                    )
                    .ConfigureAwait(false);

                if (updated is not null)
                    logger.LogInformation(
                        "The plugin {Key} updated {Count} external {Kind}",
                        pluginKey,
                        updated.ChangeCount,
                        kind.Noun
                    );
            }
        }

        var current = await kind.Values([.. wanted.Keys], ct).ConfigureAwait(false);

        foreach (var (key, value) in wanted)
        {
            if (mine.Remove(key, out var row))
                row.Value = value;
            else if (
                current.TryGetValue(key, out var held)
                && held == value
                && pluginKey.Length <= GamedataPluginValueEntity.PLUGIN_KEY_MAX_LENGTH
            )
                dbCtx.GamedataPluginValues.Add(
                    new GamedataPluginValueEntity
                    {
                        PluginKey = pluginKey,
                        RecordType = kind.Type,
                        Key = key,
                        Value = value,
                    }
                );
        }

        // The ones it brings no more: taken away above while unchanged, the hotel's otherwise.
        dbCtx.GamedataPluginValues.RemoveRange(mine.Values);

        if (await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false) > 0)
            Invalidate();
    }

    /// <summary>
    /// The keys of one kind still as the plugin gave them: the value it last gave, or for one it
    /// brings that was never kept as its own, the value in its folder.
    /// </summary>
    private async Task<IEnumerable<PluginDataItem>> UnchangedAsync(
        string pluginKey,
        Kind kind,
        IReadOnlyDictionary<string, string> seed,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var owned = await dbCtx
            .GamedataPluginValues.AsNoTracking()
            .Where(x => x.RecordType == kind.Type)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var given = seed.Where(x =>
                !owned.Any(o => o.Key == x.Key && !IsPlugin(o.PluginKey, pluginKey))
            )
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        foreach (var row in owned.Where(x => IsPlugin(x.PluginKey, pluginKey)))
            given[row.Key] = row.Value;

        if (given.Count == 0)
            return [];

        var values = await kind.Values([.. given.Keys], ct).ConfigureAwait(false);

        return given
            .Where(x => values.TryGetValue(x.Key, out var value) && value == x.Value)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new PluginDataItem(kind.DataKind, x.Key, 1, x.Value));
    }

    /// <summary>The client's files are written again: what a plugin owns, or whether it is loaded, changed.</summary>
    private void Invalidate()
    {
        files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
        files.Invalidate(GamedataFiles.EXTERNAL_TEXTS);
    }

    private static bool IsPlugin(string owner, string pluginKey) =>
        string.Equals(owner, pluginKey, StringComparison.OrdinalIgnoreCase);

    private Kind Variables =>
        new(
            GamedataRecordType.Variable,
            PluginDataKind.Variable,
            "variables",
            variables.GetValuesAsync,
            variables.SeedAsync,
            variables.ReplaceUnchangedAsync
        );

    private Kind Texts =>
        new(
            GamedataRecordType.Text,
            PluginDataKind.Text,
            "texts",
            texts.GetValuesAsync,
            texts.SeedAsync,
            texts.ReplaceUnchangedAsync
        );

    /// <summary>
    /// What the plugin's folder brings, each file read as the panel's import reads it, a variable's
    /// value as the hotel keeps it; a file that is not there brings nothing, one that can't be read
    /// is logged and brings nothing.
    /// </summary>
    private async Task<(
        IReadOnlyDictionary<string, string> Variables,
        IReadOnlyDictionary<string, string> Texts
    )> ReadAsync(string pluginKey, string folder, CancellationToken ct)
    {
        var variablesFile = Path.Combine(folder, FOLDER, VARIABLES_FILE);
        var textsFile = Path.Combine(folder, FOLDER, TEXTS_FILE);
        IReadOnlyDictionary<string, string> variableSeed = new Dictionary<string, string>();
        IReadOnlyDictionary<string, string> textSeed = new Dictionary<string, string>();

        if (File.Exists(variablesFile))
        {
            try
            {
                var parsed = new Dictionary<string, string>(StringComparer.Ordinal);

                // A key given twice keeps its last value, as the client's own parse does.
                foreach (
                    var (key, value) in ExternalVariablesFile.Parse(
                        await File.ReadAllTextAsync(variablesFile, ct).ConfigureAwait(false)
                    )
                )
                    parsed[key] = ExternalVariablesFile.Normalize(value);

                variableSeed = parsed;
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(
                    "The plugin {Key}'s {File} can't be read, so it adds no variables: {Message}",
                    pluginKey,
                    $"{FOLDER}/{VARIABLES_FILE}",
                    ex.Message
                );
            }
        }

        if (File.Exists(textsFile))
            textSeed = ExternalTextsFile.Parse(
                await File.ReadAllTextAsync(textsFile, ct).ConfigureAwait(false)
            );

        return (variableSeed, textSeed);
    }

    private static Dictionary<string, string> Values(
        ImmutableArray<PluginDataItem> found,
        PluginDataKind kind
    ) =>
        found
            .Where(x => x.Kind == kind)
            .ToDictionary(x => x.Name, x => x.Detail ?? string.Empty, StringComparer.Ordinal);

    /// <summary>One kind of what a plugin brings, and the service that keeps it.</summary>
    private sealed record Kind(
        GamedataRecordType Type,
        PluginDataKind DataKind,
        string Noun,
        Func<
            IReadOnlyCollection<string>,
            CancellationToken,
            Task<IReadOnlyDictionary<string, string>>
        > Values,
        Func<
            IReadOnlyDictionary<string, string>,
            string,
            CancellationToken,
            Task<GamedataChangeSetSnapshot?>
        > Seed,
        Func<
            IReadOnlyDictionary<string, GamedataValueChange>,
            string,
            CancellationToken,
            Task<GamedataChangeSetSnapshot?>
        > Replace
    );
}
