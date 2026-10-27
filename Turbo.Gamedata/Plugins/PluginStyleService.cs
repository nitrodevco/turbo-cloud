using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;

namespace Turbo.Gamedata.Plugins;

/// <summary>
/// <see cref="IPluginStyles"/> over the hotel's own entries of <c>templates.styles</c> and of the
/// variables a style also goes to (<see cref="ThemeStyles.TargetsOf"/>: <c>asset.overlays</c>,
/// <c>frame.overrides</c>, ...), written through the variables service as the panel's edits are. The
/// theme a style file extends is found among the server's plugins (<see cref="IHotelDataPlugin"/>).
/// </summary>
internal sealed partial class PluginStyleService(
    IGamedataVariableService variables,
    IServiceProvider? host = null
) : IPluginStyles
{
    /// <summary>The bitmaps one skin set replaces at most: the theme sheet has about 600.</summary>
    private const int MAX_OVERLAYS = 1000;

    private const int MAX_URL = 2048;

    /// <summary>The texts one change sets at most, and a text key's and a text's length.</summary>
    private const int MAX_TEXTS = 1000;

    private const int MAX_TEXT_KEY = 255;

    private const int MAX_TEXT = 4000;

    private const string WWWROOT = "wwwroot";

    private static readonly JsonDocumentOptions READ = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonSerializerOptions WRITE = new() { WriteIndented = true };

    public async Task<ImmutableArray<PluginStyle>> ListAsync(
        string pluginKey,
        string folder,
        CancellationToken ct
    )
    {
        var properties = Read(pluginKey, folder);

        if (properties.IsEmpty)
            return [];

        var hotel = await HotelAsync(ExternalVariablesFile.TEMPLATE_STYLES, ct)
            .ConfigureAwait(false);

        return [.. properties.Select(x => Of(pluginKey, x, hotel))];
    }

    public PluginTheme? ThemeOf(string pluginKey, string? folder) =>
        ThemeStyles.ThemeOf(pluginKey, folder);

    public async Task<PluginStyle> SetAsync(
        string pluginKey,
        string folder,
        string name,
        string value,
        PlayerId actor,
        CancellationToken ct
    )
    {
        var property =
            Find(pluginKey, folder, name)
            ?? throw new ArgumentException($"The theme has no style {name}.", nameof(name));
        // A path is one of the plugin's served files (an image on its Theme tab), at its address by hash.
        var client = ThemeStyles.ClientValue(
            property,
            value.Trim(),
            path => host?.GetService<IClientModuleDirectory>()?.AddressOf(pluginKey, path)
        );

        var styles = await HotelAsync(ExternalVariablesFile.TEMPLATE_STYLES, ct)
            .ConfigureAwait(false);

        styles[ThemeStyles.EntryOf(pluginKey, name)] = client;
        await SaveAsync(ExternalVariablesFile.TEMPLATE_STYLES, styles, actor, ct)
            .ConfigureAwait(false);

        foreach (var target in ThemeStyles.TargetsOf(property))
        {
            var entries = await HotelAsync(target[0], ct).ConfigureAwait(false);

            ThemeStyles.SetPath(entries, target, ThemeStyles.TargetValue(property, client));
            await SaveAsync(target[0], entries, actor, ct).ConfigureAwait(false);
        }

        return Of(pluginKey, property, styles);
    }

    public async Task<PluginStyle?> ResetAsync(
        string pluginKey,
        string folder,
        string name,
        PlayerId actor,
        CancellationToken ct
    )
    {
        if (Find(pluginKey, folder, name) is not { } property)
            return null;

        var styles = await HotelAsync(ExternalVariablesFile.TEMPLATE_STYLES, ct)
            .ConfigureAwait(false);

        if (styles.Remove(ThemeStyles.EntryOf(pluginKey, name)))
            await SaveAsync(ExternalVariablesFile.TEMPLATE_STYLES, styles, actor, ct)
                .ConfigureAwait(false);

        foreach (var target in ThemeStyles.TargetsOf(property))
        {
            var entries = await HotelAsync(target[0], ct).ConfigureAwait(false);

            if (ThemeStyles.RemovePath(entries, target))
                await SaveAsync(target[0], entries, actor, ct).ConfigureAwait(false);
        }

        return Of(pluginKey, property, styles);
    }

    public async Task<ImmutableDictionary<string, string>> TextsAsync(
        string folder,
        CancellationToken ct
    )
    {
        var path = Path.Combine(folder, PluginGamedataSeeds.FOLDER, PluginGamedataSeeds.TEXTS_FILE);

        return File.Exists(path)
            ? ExternalTextsFile
                .Parse(await File.ReadAllTextAsync(path, ct).ConfigureAwait(false))
                .ToImmutableDictionary(StringComparer.Ordinal)
            : ImmutableDictionary<string, string>.Empty;
    }

    public async Task<ImmutableDictionary<string, string>> SetTextsAsync(
        string pluginKey,
        string folder,
        IReadOnlyDictionary<string, string?> texts,
        CancellationToken ct
    )
    {
        var problems = new List<string>();

        // The hotel's own texts are its rows (Gamedata > Texts); only a theme has a texts file of its own.
        if (pluginKey == IHotelDataPlugin.KEY)
            problems.Add(
                "The hotel's texts are changed on Gamedata > Texts, not in a theme's files."
            );

        if (texts.Count == 0)
            problems.Add("Name at least one text.");

        if (texts.Count > MAX_TEXTS)
            problems.Add($"At most {MAX_TEXTS} texts at once.");

        foreach (var (key, value) in texts)
        {
            // As Habbo's file reads a line: a key ends at its first '=' and a line at a line break.
            if (
                key.Length is 0 or > MAX_TEXT_KEY
                || key.Trim() != key
                || key.IndexOfAny(['=', '\r', '\n']) >= 0
                || key.StartsWith('#')
            )
                problems.Add($"{key} is no text key.");
            else if (
                value is not null
                && (value.Length > MAX_TEXT || value.IndexOfAny(['\r', '\n']) >= 0)
            )
                problems.Add(
                    $"{key}: a text is one line of at most {MAX_TEXT} characters (a line break is written \\n)."
                );
        }

        if (problems.Count > 0)
            throw new ArgumentException(string.Join(" ", problems), nameof(texts));

        var path = Path.Combine(folder, PluginGamedataSeeds.FOLDER, PluginGamedataSeeds.TEXTS_FILE);
        var own = File.Exists(path)
            ? ExternalTextsFile.Parse(await File.ReadAllTextAsync(path, ct).ConfigureAwait(false))
            : new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in texts)
        {
            if (value is null)
                own.Remove(key);
            else
                own[key] = value;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(
                path,
                ExternalTextsFile.Write(own.Select(x => (x.Key, x.Value))),
                ct
            )
            .ConfigureAwait(false);

        return own.ToImmutableDictionary(StringComparer.Ordinal);
    }

    public async Task<ImmutableDictionary<string, string>> SetOverlaysAsync(
        string pluginKey,
        string folder,
        IReadOnlyDictionary<string, string?> overlays,
        PlayerId actor,
        CancellationToken ct
    )
    {
        var own = pluginKey != IHotelDataPlugin.KEY;

        // A theme's files name its own images by their path in its wwwroot, as its author writes them.
        if (own)
            overlays = overlays.ToDictionary(
                x => x.Key,
                x => x.Value is null ? null : OwnPath(pluginKey, x.Value),
                StringComparer.Ordinal
            );

        var problems = new List<string>();

        if (overlays.Count == 0)
            problems.Add("Name at least one bitmap.");

        if (overlays.Count > MAX_OVERLAYS)
            problems.Add($"At most {MAX_OVERLAYS} bitmaps at once.");

        foreach (var (key, url) in overlays)
        {
            if (!OverlayKeyPattern().IsMatch(key))
                problems.Add($"{key} is no bitmap: <library>/<asset>.");
            else if (
                url is not null
                && (
                    url.Length > MAX_URL
                    || !(ThemeStyles.IsUrl(url) || (own && IsOwnFile(folder, url)))
                )
            )
                problems.Add($"{key}: {url} is no address (/a/path on the hotel, or https://...).");
        }

        if (problems.Count > 0)
            throw new ArgumentException(string.Join(" ", problems), nameof(overlays));

        if (own)
            return await SetOwnOverlaysAsync(pluginKey, folder, overlays, ct).ConfigureAwait(false);

        // The hotel theme's image styles by the bitmap each stands in for: those go through the style.
        var styled = Read(pluginKey, folder)
            .Where(x => x is { Type: ThemeStyleType.Image, Overlay: not null })
            .GroupBy(x => x.Overlay!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var styles = await HotelAsync(ExternalVariablesFile.TEMPLATE_STYLES, ct)
            .ConfigureAwait(false);
        var entries = await HotelAsync(ExternalVariablesFile.ASSET_OVERLAYS, ct)
            .ConfigureAwait(false);
        var stylesChanged = false;

        foreach (var (key, url) in overlays)
        {
            if (styled.TryGetValue(key, out var property))
            {
                var entry = ThemeStyles.EntryOf(IHotelDataPlugin.KEY, property.Name);

                if (url is null)
                    stylesChanged |= styles.Remove(entry);
                else
                {
                    styles[entry] = ThemeStyles.ClientValue(property, url, _ => null);
                    stylesChanged = true;
                }
            }

            if (url is null)
                entries.Remove(key);
            else
                entries[key] = JsonValue.Create(url);
        }

        if (stylesChanged)
            await SaveAsync(ExternalVariablesFile.TEMPLATE_STYLES, styles, actor, ct)
                .ConfigureAwait(false);

        await SaveAsync(ExternalVariablesFile.ASSET_OVERLAYS, entries, actor, ct)
            .ConfigureAwait(false);

        return entries
            .Where(x => x.Value is JsonValue)
            .ToImmutableDictionary(x => x.Key, x => x.Value!.ToString(), StringComparer.Ordinal);
    }

    public async Task SetFontAsync(
        string pluginKey,
        string folder,
        string family,
        string? url,
        PlayerId actor,
        CancellationToken ct
    )
    {
        var own = pluginKey != IHotelDataPlugin.KEY;

        if (own && url is not null)
            url = OwnPath(pluginKey, url);

        if (!FamilyPattern().IsMatch(family))
            throw new ArgumentException(
                $"\"{family}\" is no font family: letters, digits, '-', '_' and spaces.",
                nameof(family)
            );

        if (
            url is not null
            && (
                url.Length > MAX_URL || !(ThemeStyles.IsUrl(url) || (own && IsOwnFile(folder, url)))
            )
        )
            throw new ArgumentException(
                $"{url} is no address (/a/path on the hotel, or https://...).",
                nameof(url)
            );

        if (own)
        {
            await ChangeVariablesAsync(
                    folder,
                    ExternalVariablesFile.TEXT_FONTS,
                    entries =>
                    {
                        if (url is null)
                            entries.Remove(family);
                        else
                            entries[family] = url;
                    },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        var fonts = await HotelAsync(ExternalVariablesFile.TEXT_FONTS, ct).ConfigureAwait(false);

        if (url is not null)
            fonts[family] = JsonValue.Create(url);
        else if (!fonts.Remove(family))
            return;

        await SaveAsync(ExternalVariablesFile.TEXT_FONTS, fonts, actor, ct).ConfigureAwait(false);
    }

    public async Task<PluginStyle> SetDefaultAsync(
        string pluginKey,
        string folder,
        string name,
        string? value,
        CancellationToken ct
    )
    {
        var property =
            Find(pluginKey, folder, name)
            ?? throw new ArgumentException($"The theme has no style {name}.", nameof(name));

        if (value is not null)
        {
            value = OwnPath(pluginKey, value.Trim());

            // Checked as clients would be given it: an image is a file of its wwwroot or an address.
            ThemeStyles.ClientValue(property, value, path => IsOwnFile(folder, path) ? path : null);
        }

        await ChangeThemeAsync(
                folder,
                styles =>
                {
                    if (styles[name] is not JsonObject declared)
                        styles[name] = declared = [];

                    if (value is null)
                        declared.Remove("default");
                    else
                        declared["default"] = value;

                    if (declared.Count == 0)
                        styles.Remove(name);
                },
                ct
            )
            .ConfigureAwait(false);

        var hotel = await HotelAsync(ExternalVariablesFile.TEMPLATE_STYLES, ct)
            .ConfigureAwait(false);

        return Of(pluginKey, Find(pluginKey, folder, name)!, hotel);
    }

    /// <summary>
    /// A theme's skin set, into its own files: a bitmap an image style of its chain stands in for
    /// becomes that style's default in its <c>theme.json</c>, the rest its entries of
    /// <c>asset.overlays</c> in its <c>gamedata/external_variables.json</c>. Answers those entries after.
    /// </summary>
    private async Task<ImmutableDictionary<string, string>> SetOwnOverlaysAsync(
        string pluginKey,
        string folder,
        IReadOnlyDictionary<string, string?> overlays,
        CancellationToken ct
    )
    {
        var styled = Read(pluginKey, folder)
            .Where(x => x is { Type: ThemeStyleType.Image, Overlay: not null })
            .GroupBy(x => x.Overlay!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var defaults = overlays.Where(x => styled.ContainsKey(x.Key)).ToList();

        if (defaults.Count > 0)
            await ChangeThemeAsync(
                    folder,
                    styles =>
                    {
                        foreach (var (key, url) in defaults)
                        {
                            var name = styled[key].Name;

                            if (styles[name] is not JsonObject declared)
                                styles[name] = declared = [];

                            if (url is null)
                                declared.Remove("default");
                            else
                                declared["default"] = url;

                            if (declared.Count == 0)
                                styles.Remove(name);
                        }
                    },
                    ct
                )
                .ConfigureAwait(false);

        return await ChangeVariablesAsync(
                folder,
                ExternalVariablesFile.ASSET_OVERLAYS,
                entries =>
                {
                    foreach (var (key, url) in overlays)
                        if (url is null)
                            entries.Remove(key);
                        else
                            entries[key] = url;
                },
                ct
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Changes the <c>styles</c> of the theme's <c>theme.json</c> in <paramref name="folder"/>
    /// (made, extending the base theme, when there is none) and writes it back.
    /// </summary>
    private static async Task ChangeThemeAsync(
        string folder,
        Action<JsonObject> change,
        CancellationToken ct
    )
    {
        var path = Path.Combine(folder, ThemeStyles.FILE);
        var root = File.Exists(path)
            ? JsonNode.Parse(
                await File.ReadAllTextAsync(path, ct).ConfigureAwait(false),
                documentOptions: READ
            ) as JsonObject
                ?? throw new ArgumentException($"{ThemeStyles.FILE} is no object.", nameof(folder))
            : new JsonObject { ["extends"] = ThemeStyles.BASE };

        if (root["styles"] is not JsonObject styles)
            root["styles"] = styles = [];

        change(styles);
        await File.WriteAllTextAsync(path, root.ToJsonString(WRITE), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Changes the entries of one merged variable in the theme's <c>gamedata/external_variables.json</c>
    /// in <paramref name="folder"/> (made when there is none) and writes it back; answers its entries
    /// after (the variable goes with the last).
    /// </summary>
    private static async Task<ImmutableDictionary<string, string>> ChangeVariablesAsync(
        string folder,
        string variable,
        Action<Dictionary<string, string>> change,
        CancellationToken ct
    )
    {
        var path = Path.Combine(
            folder,
            PluginGamedataSeeds.FOLDER,
            PluginGamedataSeeds.VARIABLES_FILE
        );
        var root = File.Exists(path)
            ? JsonNode.Parse(
                await File.ReadAllTextAsync(path, ct).ConfigureAwait(false),
                documentOptions: READ
            ) as JsonObject
                ?? throw new ArgumentException(
                    $"{PluginGamedataSeeds.VARIABLES_FILE} is no object.",
                    nameof(folder)
                )
            : [];
        var entries = ThemeStyles
            .Entries(root[variable]?.ToJsonString())
            .Where(x => x.Value is JsonValue value && value.TryGetValue<string>(out _))
            .ToDictionary(x => x.Key, x => x.Value!.GetValue<string>(), StringComparer.Ordinal);

        change(entries);

        if (entries.Count == 0)
            root.Remove(variable);
        else
            root[variable] = new JsonObject(
                entries
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => KeyValuePair.Create(x.Key, (JsonNode?)JsonValue.Create(x.Value)))
            );

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, root.ToJsonString(WRITE), ct).ConfigureAwait(false);

        return entries.ToImmutableDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// A value naming one of the theme's own served files by its address
    /// (<c>/gamedata/plugins/&lt;key&gt;/styles/a.png?v=...</c>, as an upload answers) as its path in
    /// its wwwroot (<c>styles/a.png</c>), as its files name it wherever it is installed; any other as it is.
    /// </summary>
    private static string OwnPath(string pluginKey, string value)
    {
        var prefix = $"/gamedata/plugins/{pluginKey}/";
        var at = value.IndexOf(prefix, StringComparison.Ordinal);

        return at < 0 ? value : Uri.UnescapeDataString(value[(at + prefix.Length)..].Split('?')[0]);
    }

    /// <summary>Whether <paramref name="path"/> is a file of the plugin's wwwroot in <paramref name="folder"/>.</summary>
    private static bool IsOwnFile(string folder, string path)
    {
        if (
            path.Length == 0
            || path.StartsWith('/')
            || path.Contains("://", StringComparison.Ordinal)
        )
            return false;

        var root = Path.GetFullPath(Path.Combine(folder, WWWROOT));
        var full = Path.GetFullPath(Path.Combine(root, path));

        return full.StartsWith(
                root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            ) && File.Exists(full);
    }

    private ImmutableArray<ThemeStyleProperty> Read(string pluginKey, string folder) =>
        ThemeStyles.Read(
            pluginKey,
            folder,
            key => host?.GetService<IHotelDataPlugin>()?.FolderOf(key)
        );

    private ThemeStyleProperty? Find(string pluginKey, string folder, string name) =>
        Read(pluginKey, folder).FirstOrDefault(x => x.Name == name);

    private static PluginStyle Of(
        string pluginKey,
        ThemeStyleProperty property,
        Dictionary<string, JsonNode?> hotel
    ) =>
        hotel.TryGetValue(ThemeStyles.EntryOf(pluginKey, property.Name), out var value)
            ? new PluginStyle(property, ThemeStyles.FromClient(property, value), true)
            : new PluginStyle(property, property.Default, false);

    /// <summary>The hotel's own entries of a merged variable: what staff set, never a plugin's.</summary>
    private async Task<Dictionary<string, JsonNode?>> HotelAsync(string key, CancellationToken ct)
    {
        var values = await variables.GetValuesAsync([key], ct).ConfigureAwait(false);

        return ThemeStyles.Entries(values.GetValueOrDefault(key));
    }

    /// <summary>The hotel's entries written back; none left takes the variable away.</summary>
    private async Task SaveAsync(
        string key,
        Dictionary<string, JsonNode?> entries,
        PlayerId actor,
        CancellationToken ct
    )
    {
        if (entries.Count == 0)
        {
            await variables.DeleteAsync(key, actor, ct).ConfigureAwait(false);

            return;
        }

        var value = new JsonObject();

        foreach (var (entry, node) in entries.OrderBy(x => x.Key, StringComparer.Ordinal))
            value[entry] = node?.DeepClone();

        await variables.SaveAsync(key, value.ToJsonString(), actor, ct).ConfigureAwait(false);
    }

    [GeneratedRegex(@"^[A-Za-z0-9_.\-]+/[A-Za-z0-9_.\-]+$")]
    private static partial Regex OverlayKeyPattern();

    [GeneratedRegex(@"^[A-Za-z0-9_\-][A-Za-z0-9_\- ]{0,63}$")]
    private static partial Regex FamilyPattern();
}
