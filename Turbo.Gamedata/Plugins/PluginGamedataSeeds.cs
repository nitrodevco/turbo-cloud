using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;

namespace Turbo.Gamedata.Plugins;

/// <summary>
/// A plugin's own external variables and texts (<see cref="IPluginGamedataSeeds"/>): given to the
/// hotel as the plugin loads when the hotel has none of the key, through the same services the
/// panel's edits and imports go through, each load one change set by nobody. A key the hotel has,
/// its own or changed since, is never touched. On an uninstall with its data
/// (<see cref="IPluginDataOwner"/>) the ones still as the plugin gave them go; the hotel's changes stay.
/// </summary>
internal sealed class PluginGamedataSeeds(
    IGamedataVariableService variables,
    IGamedataTextService texts,
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

        if (variableSeed.Count > 0)
        {
            var added = await variables
                .SeedAsync(variableSeed, $"The plugin {pluginKey} added its variables", ct)
                .ConfigureAwait(false);

            if (added is not null)
                logger.LogInformation(
                    "The plugin {Key} added {Count} external variables",
                    pluginKey,
                    added.ChangeCount
                );
        }

        if (textSeed.Count > 0)
        {
            var added = await texts
                .SeedAsync(textSeed, $"The plugin {pluginKey} added its texts", ct)
                .ConfigureAwait(false);

            if (added is not null)
                logger.LogInformation(
                    "The plugin {Key} added {Count} external texts",
                    pluginKey,
                    added.ChangeCount
                );
        }
    }

    public async Task<ImmutableArray<PluginDataItem>> FindAsync(
        PluginDataScope plugin,
        CancellationToken ct
    )
    {
        if (plugin.Folder is null)
            return [];

        var (variableSeed, textSeed) = await ReadAsync(plugin.Key, plugin.Folder, ct)
            .ConfigureAwait(false);
        var items = ImmutableArray.CreateBuilder<PluginDataItem>();

        items.AddRange(
            await UnchangedAsync(
                    PluginDataKind.Variable,
                    variableSeed,
                    variables.GetValuesAsync,
                    ct
                )
                .ConfigureAwait(false)
        );
        items.AddRange(
            await UnchangedAsync(PluginDataKind.Text, textSeed, texts.GetValuesAsync, ct)
                .ConfigureAwait(false)
        );

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
    }

    /// <summary>
    /// What the plugin's folder brings, each file read as the panel's import reads it; a file that
    /// is not there brings nothing, one that can't be read is logged and brings nothing.
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
                    parsed[key] = value;

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

    private static async Task<IEnumerable<PluginDataItem>> UnchangedAsync(
        PluginDataKind kind,
        IReadOnlyDictionary<string, string> seed,
        Func<
            IReadOnlyCollection<string>,
            CancellationToken,
            Task<IReadOnlyDictionary<string, string>>
        > current,
        CancellationToken ct
    )
    {
        if (seed.Count == 0)
            return [];

        var values = await current([.. seed.Keys], ct).ConfigureAwait(false);

        return seed.Where(x => values.TryGetValue(x.Key, out var value) && value == x.Value)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new PluginDataItem(kind, x.Key, 1, x.Value));
    }

    private static Dictionary<string, string> Values(
        ImmutableArray<PluginDataItem> found,
        PluginDataKind kind
    ) =>
        found
            .Where(x => x.Kind == kind)
            .ToDictionary(x => x.Name, x => x.Detail ?? string.Empty, StringComparer.Ordinal);
}
