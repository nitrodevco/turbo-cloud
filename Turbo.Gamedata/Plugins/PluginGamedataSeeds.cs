using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Gamedata;
using Turbo.Gamedata.ClientModules;
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
    ClientModuleRegistry modules,
    ILogger<PluginGamedataSeeds> logger,
    IServiceProvider? host = null
) : IPluginGamedataSeeds, IPluginDataOwner
{
    /// <summary>The folder in a plugin's own that holds what it brings of the client's gamedata.</summary>
    public const string FOLDER = "gamedata";

    /// <summary>Its external variables: a JSON object, as a client config is.</summary>
    public const string VARIABLES_FILE = "external_variables.json";

    /// <summary>Its external texts: <c>key=value</c> lines, as Habbo's file is.</summary>
    public const string TEXTS_FILE = "external_texts.txt";

    /// <summary>
    /// Its window patches (nitro-next <c>docs/template-patches.md</c>), in its <c>wwwroot/</c>: listed
    /// for clients in <see cref="ExternalVariablesFile.TEMPLATE_PATCHES"/> under the plugin's key
    /// while it is loaded, without the plugin naming it anywhere.
    /// </summary>
    public const string TEMPLATE_PATCHES_FILE = "template-patches.json";

    private const string WWWROOT = "wwwroot";

    public async Task SeedAsync(string pluginKey, string folder, CancellationToken ct)
    {
        var (variableSeed, textSeed) = await ReadAsync(pluginKey, folder, ct).ConfigureAwait(false);

        // Its parts of the merged variables are written into the client's file beside the
        // hotel's own while it is loaded, never given to the hotel: a key it gave before as its
        // own goes below as one it brings no more.
        var parts = variableSeed
            .Where(x => ExternalVariablesFile.IsMerged(x.Key))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        // A patch file in its wwwroot is its entry of the patches list, by its own key, so two
        // plugins' entries never meet; one it names itself in its variables comes first.
        if (
            !parts.ContainsKey(ExternalVariablesFile.TEMPLATE_PATCHES)
            && modules.AddressOf(pluginKey, Path.Combine(folder, WWWROOT), TEMPLATE_PATCHES_FILE)
                is { } patches
        )
            parts[ExternalVariablesFile.TEMPLATE_PATCHES] = new JsonObject
            {
                [pluginKey] = patches,
            }.ToJsonString();

        if (parts.TryGetValue(ExternalVariablesFile.ASSET_OVERLAYS, out var overlays))
            parts[ExternalVariablesFile.ASSET_OVERLAYS] = AddressEntries(
                pluginKey,
                Path.Combine(folder, WWWROOT),
                overlays,
                "overlay of",
                "the library's own is used"
            );

        if (parts.TryGetValue(ExternalVariablesFile.TEXT_FONTS, out var fonts))
            parts[ExternalVariablesFile.TEXT_FONTS] = AddressEntries(
                pluginKey,
                Path.Combine(folder, WWWROOT),
                fonts,
                "font",
                "the family is not loaded"
            );

        if (parts.TryGetValue(ExternalVariablesFile.LOADING_SCREEN, out var loadingScreen))
            parts[ExternalVariablesFile.LOADING_SCREEN] = AddressLoadingScreen(
                pluginKey,
                Path.Combine(folder, WWWROOT),
                loadingScreen
            );

        AddStyles(pluginKey, folder, parts);

        loaded.SetParts(pluginKey, parts);
        Invalidate();

        await FollowAsync(
                pluginKey,
                Variables,
                variableSeed
                    .Where(x => !ExternalVariablesFile.IsMerged(x.Key))
                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
                ct
            )
            .ConfigureAwait(false);
        await FollowAsync(pluginKey, Texts, textSeed, ct).ConfigureAwait(false);
        await SeedChatStylesAsync(pluginKey, folder, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The chat bubbles in its <see cref="PluginChatStyles.FOLDER"/>, each one the hotel has none of
    /// added; one that can't be kept (a bitmap that isn't an image) is logged, the plugin loads.
    /// </summary>
    private async Task SeedChatStylesAsync(string pluginKey, string folder, CancellationToken ct)
    {
        if (host?.GetService<IChatStyleService>() is not { } chatStyles)
            return;

        try
        {
            var added = await PluginChatStyles
                .SeedAsync(chatStyles, PluginChatStyles.Read(folder), ct)
                .ConfigureAwait(false);

            if (added.Count > 0)
                logger.LogInformation(
                    "Plugin {Key} added the chat styles {Styles} from its {Folder} folder",
                    pluginKey,
                    string.Join(", ", added),
                    PluginChatStyles.FOLDER
                );
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or JsonException)
        {
            logger.LogError(
                ex,
                "Plugin {Key}'s chat styles ({Folder}/{File}) were not added",
                pluginKey,
                PluginChatStyles.FOLDER,
                PluginChatStyles.CATALOGUE
            );
        }
    }

    public IDisposable Loaded(string pluginKey) => loaded.Add(pluginKey, Invalidate);

    /// <summary>
    /// A plugin's <see cref="ExternalVariablesFile.ASSET_OVERLAYS"/> or <see cref="ExternalVariablesFile.TEXT_FONTS"/>:
    /// a file it names by its path in its <c>wwwroot/</c> (<c>"frame.png"</c>, <c>"styles/pixel.woff2"</c>)
    /// is given to clients at its address by hash, as its patch file is; a url (<c>/...</c>,
    /// <c>https://...</c>) is kept as it is. One whose file is not there is logged and left out, so the
    /// library's own bitmap stays (a font's family is not loaded).
    /// </summary>
    private string AddressEntries(
        string pluginKey,
        string wwwroot,
        string json,
        string what,
        string outcome
    )
    {
        if (ParseObject(json) is not { } entries)
            return json;

        var addressed = new JsonObject();

        foreach (var (entry, value) in entries)
        {
            if (Address(pluginKey, wwwroot, value) is { } node)
                addressed[entry] = node;
            else
                logger.LogWarning(
                    "The plugin {Key}'s {What} {Entry} names {Path}, which is no file of its wwwroot: {Outcome}",
                    pluginKey,
                    what,
                    entry,
                    value?.ToString(),
                    outcome
                );
        }

        return addressed.ToJsonString();
    }

    /// <summary>
    /// A plugin's <see cref="ExternalVariablesFile.LOADING_SCREEN"/>: its pictures (<c>backgroundImage</c>,
    /// <c>logo</c>, each of <c>layers</c>' <c>image</c>) named by their path in its <c>wwwroot/</c> are
    /// given to clients at their address, as its overlays are; one whose file is not there is logged and
    /// left out (the screen is drawn without it).
    /// </summary>
    private string AddressLoadingScreen(string pluginKey, string wwwroot, string json)
    {
        if (ParseObject(json) is not { } look)
            return json;

        void AddressIn(JsonObject owner, string key, string part)
        {
            if (!owner.TryGetPropertyValue(key, out var value))
                return;

            if (Address(pluginKey, wwwroot, value) is { } node)
            {
                owner[key] = node;

                return;
            }

            owner.Remove(key);
            logger.LogWarning(
                "The plugin {Key}'s loading screen {Part} names {Path}, which is no file of its wwwroot: it is left out",
                pluginKey,
                part,
                value?.ToString()
            );
        }

        AddressIn(look, "backgroundImage", "backgroundImage");
        AddressIn(look, "logo", "logo");

        if (look["layers"] is JsonArray layers)
            for (var i = 0; i < layers.Count; i++)
                if (layers[i] is JsonObject layer)
                    AddressIn(layer, "image", $"layers[{i}]");

        return look.ToJsonString();
    }

    /// <summary>A merged variable's value as an object: the object, or the object as JSON text.</summary>
    private static JsonObject? ParseObject(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);

            if (node is JsonValue text && text.TryGetValue<string>(out var inner))
                node = JsonNode.Parse(inner);

            return node as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// A picture a plugin names: by its path in its <c>wwwroot/</c> (<c>"frame.png"</c>), its address by
    /// hash, as its patch file's; a url (<c>/...</c>, <c>https://...</c>) or a value that is no text as
    /// it is; <see langword="null"/> for a path that is no file of its wwwroot.
    /// </summary>
    private JsonNode? Address(string pluginKey, string wwwroot, JsonNode? value)
    {
        if (
            value is not JsonValue url
            || !url.TryGetValue<string>(out var path)
            || path.Length == 0
            || path.StartsWith('/')
            || path.Contains("://", StringComparison.Ordinal)
        )
            return value?.DeepClone();

        return modules.AddressOf(pluginKey, wwwroot, path) is { } address
            ? JsonValue.Create(address)
            : null;
    }

    /// <summary>
    /// A theme's style properties (<see cref="ThemeStyles"/>): their defaults are its entries of
    /// <see cref="ExternalVariablesFile.TEMPLATE_STYLES"/>, and its entries of the variables each
    /// also goes to (<see cref="ThemeStyles.TargetsOf"/>: an image standing in for a library bitmap
    /// <see cref="ExternalVariablesFile.ASSET_OVERLAYS"/>, a frame colour <c>frame.overrides</c>),
    /// where it names nothing there itself. A property with no default (the base theme's, until a
    /// theme that extends it gives one) gives nothing. A <c>theme.json</c> that can't be read is
    /// logged and gives nothing.
    /// </summary>
    private void AddStyles(string pluginKey, string folder, Dictionary<string, string> parts)
    {
        ImmutableArray<ThemeStyleProperty> properties;

        try
        {
            properties = ThemeStyles.Read(
                pluginKey,
                folder,
                key => host?.GetService<IHotelDataPlugin>()?.FolderOf(key)
            );
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(
                "The plugin {Key}'s {File} was left out: {Problem}",
                pluginKey,
                ThemeStyles.FILE,
                ex.Message
            );

            return;
        }

        if (properties.IsEmpty)
            return;

        var wwwroot = Path.Combine(folder, WWWROOT);
        var styles = new JsonObject();
        var targets = new Dictionary<string, Dictionary<string, JsonNode?>>(StringComparer.Ordinal);

        foreach (var property in properties)
        {
            if (property.Default is not { } value)
                continue;

            try
            {
                var client = ThemeStyles.ClientValue(
                    property,
                    value,
                    path => modules.AddressOf(pluginKey, wwwroot, path)
                );

                styles[ThemeStyles.EntryOf(pluginKey, property.Name)] = client;

                foreach (var target in ThemeStyles.TargetsOf(property))
                {
                    if (!targets.TryGetValue(target[0], out var entries))
                        targets[target[0]] = entries = ThemeStyles.Entries(
                            parts.GetValueOrDefault(target[0])
                        );

                    ThemeStyles.SetPath(
                        entries,
                        target,
                        ThemeStyles.TargetValue(property, client),
                        keep: true
                    );
                }
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(
                    "The plugin {Key}'s style {Name} was left out: {Problem}",
                    pluginKey,
                    property.Name,
                    ex.Message
                );
            }
        }

        if (styles.Count > 0)
            parts[ExternalVariablesFile.TEMPLATE_STYLES] = styles.ToJsonString();

        foreach (var (variable, entries) in targets.Where(x => x.Value.Count > 0))
        {
            var value = new JsonObject();

            foreach (var (entry, node) in entries)
                value[entry] = node;

            parts[variable] = value.ToJsonString();
        }
    }

    public async Task<ImmutableArray<PluginDataItem>> FindAsync(
        PluginDataScope plugin,
        CancellationToken ct
    )
    {
        var (variableSeed, textSeed) = plugin.Folder is null
            ? (new Dictionary<string, string>(), new Dictionary<string, string>())
            : await ReadAsync(plugin.Key, plugin.Folder, ct).ConfigureAwait(false);

        variableSeed = variableSeed
            .Where(x => !ExternalVariablesFile.IsMerged(x.Key))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

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
