using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Settings;

namespace Turbo.Gamedata.Variables;

/// <summary>
/// The client's external variables as the file Nitro loads: one JSON object, a key for each
/// variable, in ordinal order. A value is kept as JSON text, written back as it is.
/// <para>
/// A variable may follow something instead of holding a value of its own (<see cref="Resolve"/>):
/// a server setting, or the address of one of the hotel's gamedata files by hash, as Habbo's
/// external variables give theirs. Following a file, the client loads exactly the build the
/// variables were built with, cached for good, with no redirect on the way.
/// </para>
/// </summary>
internal static class ExternalVariablesFile
{
    /// <summary>The files a variable may follow the address of: every one the hotel builds but this.</summary>
    public static readonly IReadOnlyList<string> LINKABLE =
    [
        GamedataFiles.FURNITURE_DATA,
        GamedataFiles.PRODUCT_DATA,
        GamedataFiles.EXTERNAL_TEXTS,
        GamedataFiles.FIGURE_DATA,
        GamedataFiles.BADGE_PARTS,
        GamedataFiles.FIGURE_MAP,
        GamedataFiles.EFFECT_MAP,
    ];

    // The file is served as JSON, never into a page, so text is written as it is: an apostrophe
    // stays an apostrophe for staff reading the panel.
    private static readonly JsonSerializerOptions TEXT = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonWriterOptions WRITER = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = true,
    };

    /// <summary>
    /// The window patch files clients load (nitro-next <c>docs/template-patches.md</c>): a url per
    /// plugin key. A plugin's <c>wwwroot/template-patches.json</c> is its entry without it naming
    /// it; the hotel takes one away with an empty url.
    /// </summary>
    public const string TEMPLATE_PATCHES = "templates.patches";

    /// <summary>
    /// The windows plugins define as data (nitro-next <c>docs/template-patches.md</c>, "Windows as
    /// data"): <c>{ "&lt;plugin key&gt;/&lt;name&gt;": "&lt;url&gt;" }</c>. Each <c>wwwroot/windows/&lt;name&gt;.json</c>
    /// of a plugin is its entry without it naming it; the hotel takes one away with an empty url.
    /// </summary>
    public const string TEMPLATE_WINDOWS = "templates.windows";

    /// <summary>The template libraries the client loads in place of its own: a url per library.</summary>
    public const string TEMPLATE_BUNDLES = "asset.bundles.templates.overrides";

    /// <summary>
    /// The style of windows the client draws with a frame of its own rather than from a template
    /// (nitro-next <c>docs/template-patches.md</c>): <c>{ "&lt;frame id or window name&gt;": { "variant": "&lt;style&gt;", "color": "#rrggbb" } }</c>.
    /// </summary>
    public const string FRAME_OVERRIDES = "frame.overrides";

    /// <summary>
    /// Single bitmaps of a template library a theme replaces (nitro-next <c>docs/template-patches.md</c>):
    /// <c>{ "&lt;library&gt;/&lt;asset&gt;": "&lt;url&gt;" }</c>. A plugin names a file of its <c>wwwroot/</c>
    /// by its path there, which clients are given its address for (<see cref="Plugins.PluginGamedataSeeds"/>).
    /// </summary>
    public const string ASSET_OVERLAYS = "asset.overlays";

    /// <summary>
    /// The values of themes' style properties their patch files read as <c>${style:name}</c>
    /// (<see cref="Plugins.ThemeStyles"/>): <c>{ "&lt;plugin key&gt;:&lt;name&gt;": value }</c>. A theme
    /// brings its defaults; the hotel's own entries are staff's changes.
    /// </summary>
    public const string TEMPLATE_STYLES = "templates.styles";

    /// <summary>
    /// The loading screen's look, read by the client before any module loads (nitro-next
    /// <c>docs/client-modules.md</c>): <c>{ "background": "#202020", "logo": "...", "bar": { ... } }</c>,
    /// each key a theme leaves out Habbo's own. Each plugin brings its keys; the hotel's own win.
    /// </summary>
    public const string LOADING_SCREEN = "loading.screen";

    /// <summary>
    /// What a theme or the hotel puts in place of the client's text styles (nitro-next
    /// <c>docs/template-patches.md</c>): <c>{ "&lt;style&gt;": "&lt;another style&gt;" }</c>, or the
    /// format fields it changes, <c>{ "u_regular": { "fontFamily": "Volter" } }</c>.
    /// </summary>
    public const string TEXT_STYLES = "text.styles.overrides";

    /// <summary>
    /// Font files a theme or the hotel gives the client (nitro-next <c>docs/text.md</c>):
    /// <c>{ "&lt;family&gt;": "&lt;url&gt;" }</c>, each loaded as that family before the first text is
    /// drawn, so a text style's <c>fontFamily</c> names it. A plugin names a file of its
    /// <c>wwwroot/</c> by its path there, as an overlay does.
    /// </summary>
    public const string TEXT_FONTS = "text.fonts";

    /// <summary>
    /// A theme's own texts (its <c>gamedata/external_texts.txt</c>): <c>{ "&lt;key&gt;": "&lt;text&gt;" }</c>,
    /// drawn by the client over the hotel's texts for the players who get the theme. A key staff
    /// changed on Gamedata &gt; Texts (the hotel's text is not Habbo's) is the hotel's: it is left out
    /// as the variables are written (<see cref="WithoutKeys"/>).
    /// </summary>
    public const string TEXT_OVERRIDES = "text.overrides";

    /// <summary>
    /// The variables a plugin brings a part of rather than the whole: a JSON object whose entries
    /// several plugins add to (<c>asset.bundles.templates.overrides</c>, a url per template
    /// library; <see cref="TEMPLATE_PATCHES"/>, a url per plugin; <see cref="TEMPLATE_WINDOWS"/>, a url per
    /// window; <see cref="FRAME_OVERRIDES"/>, a
    /// style per window; <see cref="ASSET_OVERLAYS"/>, a url per bitmap; <see cref="TEMPLATE_STYLES"/>,
    /// a value per theme style; <see cref="LOADING_SCREEN"/>, a look per key of the loading screen;
    /// <see cref="TEXT_STYLES"/>, a format per text style; <see cref="TEXT_FONTS"/>, a font file per family). Each loaded plugin's entries are written into the client's file
    /// with the hotel's own (<see cref="Merge"/>); none is ever given to the hotel.
    /// </summary>
    public static readonly IReadOnlyList<string> MERGED =
    [
        TEMPLATE_BUNDLES,
        TEMPLATE_PATCHES,
        TEMPLATE_WINDOWS,
        FRAME_OVERRIDES,
        ASSET_OVERLAYS,
        TEMPLATE_STYLES,
        LOADING_SCREEN,
        TEXT_STYLES,
        TEXT_FONTS,
        TEXT_OVERRIDES,
    ];

    /// <summary>
    /// Where a client asks, with the ticket it logs in with, for the theme its player draws and that
    /// theme's merged variables (<see cref="Plugins.ThemeChains"/>): written while a player may draw
    /// another theme than the default.
    /// </summary>
    public const string THEME_SESSION_URL = "theme.session.url";

    /// <summary>The address of the hotel's texts the client loads (the external texts file's, by its hash).</summary>
    public const string EXTERNAL_TEXTS_URL = "gamedata.urls.externalTexts";

    /// <summary>The theme route on the gamedata host, under the public address when there is one.</summary>
    public static string ThemeSessionAddress(Addresses addresses) =>
        Text($"{addresses.Root}{Api.GamedataServer.THEME_ROUTE}");

    public static bool IsLinkable(string file) => LINKABLE.Contains(file, StringComparer.Ordinal);

    public static bool IsMerged(string key) => MERGED.Contains(key, StringComparer.Ordinal);

    /// <summary>
    /// A merged variable's value (<see cref="MERGED"/>): every entry of the plugins'
    /// <paramref name="parts"/>, the first plugin's where two name the same, and the hotel's own
    /// entries over them, so a hotel's edit wins (an empty url takes the plugin's library away). An
    /// entry that is an object on both sides takes the hotel's fields over the plugin's, at every
    /// depth: staff's frame colour for the catalogue keeps the theme's first position for it.
    /// A value may be the object or the object as JSON text, as the client reads it; one that is
    /// neither adds nothing.
    /// </summary>
    public static string Merge(IEnumerable<string> parts, string? hotel)
    {
        var merged = new JsonObject();

        foreach (var part in parts)
        foreach (var (name, value) in Entries(part))
            if (!merged.ContainsKey(name))
                merged[name] = value?.DeepClone();

        if (hotel is not null)
            foreach (var (name, value) in Entries(hotel))
                merged[name] = Over(merged[name], value);

        return merged.ToJsonString(TEXT);
    }

    /// <summary>The entry names of a merged variable's part.</summary>
    public static IEnumerable<string> KeysOf(string part) => Entries(part).Select(x => x.Key);

    /// <summary>A merged variable's part without the entries <paramref name="drop"/> names: the same text when it drops none.</summary>
    public static string WithoutKeys(string part, Func<string, bool> drop)
    {
        var entries = Entries(part).ToList();

        if (!entries.Any(x => drop(x.Key)))
            return part;

        return new JsonObject(
            entries
                .Where(x => !drop(x.Key))
                .Select(x => KeyValuePair.Create(x.Key, x.Value?.DeepClone()))
        ).ToJsonString(TEXT);
    }

    /// <summary><paramref name="over"/> over <paramref name="under"/>: two objects field by field, anything else <paramref name="over"/>.</summary>
    private static JsonNode? Over(JsonNode? under, JsonNode? over)
    {
        if (under is not JsonObject below || over is not JsonObject above)
            return over?.DeepClone();

        var result = (JsonObject)below.DeepClone();

        foreach (var (name, value) in above)
            result[name] = Over(result[name], value);

        return result;
    }

    /// <summary>
    /// The entries of a merged variable that more than one plugin brings, each with the plugins
    /// that bring it in merge order: the first one's is used (<see cref="Merge"/>).
    /// </summary>
    public static IReadOnlyList<(string Entry, IReadOnlyList<string> Plugins)> Collisions(
        IEnumerable<(string Plugin, string Part)> parts
    ) =>
        [
            .. parts
                .SelectMany(x => Entries(x.Part).Select(entry => (Entry: entry.Key, x.Plugin)))
                .GroupBy(x => x.Entry, StringComparer.Ordinal)
                .Where(x =>
                    x.Select(y => y.Plugin).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1
                )
                .Select(x => (x.Key, (IReadOnlyList<string>)[.. x.Select(y => y.Plugin)])),
        ];

    private static IEnumerable<KeyValuePair<string, JsonNode?>> Entries(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);

            if (node is JsonValue text && text.TryGetValue<string>(out var inner))
                node = JsonNode.Parse(inner);

            return node is JsonObject entries ? [.. entries] : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// The value the file writes for a variable: its setting's value, or its file's address by
    /// hash, when it follows one that can be written; otherwise its own.
    /// </summary>
    public static string Resolve(
        string value,
        string? setting,
        string? file,
        Addresses addresses,
        IServerSettings settings
    ) =>
        setting is not null ? settings.GetValue(setting) ?? value
        : file is not null ? addresses.Of(file) ?? value
        : value;

    /// <summary>
    /// A file's address that never changes, <c>/gamedata/&lt;file&gt;/0</c> (it redirects to the
    /// current build), under the public address when there is one: what a variable that stops
    /// following the file keeps.
    /// </summary>
    public static string StableAddress(string publicUrl, string file) =>
        Text($"{Root(publicUrl)}/gamedata/{file}/0");

    /// <summary>
    /// A value as it is kept: JSON, written compactly. Throws <see cref="ArgumentException"/>
    /// when it isn't JSON.
    /// </summary>
    public static string Normalize(string value)
    {
        try
        {
            var node = JsonNode.Parse(value);

            return node is null ? "null" : node.ToJsonString(TEXT);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"A value is JSON: \"text\" in quotes, true, 120 or [1, 2]. {ex.Message}",
                nameof(value),
                ex
            );
        }
    }

    /// <summary>
    /// A client config's variables in its own order, each value kept as JSON; a key given twice
    /// is listed twice. Throws <see cref="ArgumentException"/> when the config isn't a JSON object.
    /// </summary>
    public static List<(string Key, string Value)> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                }
            );

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException(
                    "The config is one JSON object: { \"key\": value, ... }.",
                    nameof(json)
                );

            return
            [
                .. document
                    .RootElement.EnumerateObject()
                    .Select(x => (x.Name, Normalize(x.Value.GetRawText()))),
            ];
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"The config isn't JSON. {ex.Message}", nameof(json), ex);
        }
    }

    /// <summary>The variables as the file, by key in ordinal order, UTF-8.</summary>
    public static byte[] Write(IEnumerable<(string Key, string Value)> variables)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream, WRITER))
        {
            writer.WriteStartObject();

            foreach (var (key, value) in variables.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(key);
                writer.WriteRawValue(value);
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    /// <summary>
    /// The addresses of the files a variable may follow, by their current hash. None without
    /// <paramref name="publicUrl"/>: a build is the same on every silo, so the address can't come
    /// from a request, and a variable following a file then writes its own value.
    /// </summary>
    public static async Task<Addresses> AddressesAsync(
        IGamedataFileService files,
        string publicUrl,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(publicUrl))
            return Addresses.NONE;

        var hashes = new List<(string File, string Hash)>();

        foreach (var file in LINKABLE)
        {
            var current = await files.GetCurrentAsync(file, ct).ConfigureAwait(false);

            hashes.Add((file, current.File.Hash));
        }

        return new Addresses(Root(publicUrl), hashes);
    }

    private static string Root(string publicUrl) => publicUrl.Trim().TrimEnd('/');

    private static string Text(string text) => JsonValue.Create(text).ToJsonString(TEXT);

    /// <summary>
    /// What the file is made from besides its rows: the files' addresses, and the version of the
    /// server's settings the linked variables were read at. Two are equal when they write the
    /// same, so the file is built again only when an address has moved or a setting changed.
    /// </summary>
    public sealed record Inputs(Addresses Addresses, int SettingsVersion);

    /// <summary>
    /// The files' addresses by hash under <see cref="Root"/>; two are equal when they give the same
    /// addresses.
    /// </summary>
    public sealed record Addresses(string? Root, IReadOnlyList<(string File, string Hash)> Hashes)
    {
        public static readonly Addresses NONE = new(null, []);

        /// <summary>The file's address by its current hash, as JSON; null without a public address.</summary>
        public string? Of(string file) =>
            Root is null ? null
            : Hashes.FirstOrDefault(x => x.File == file) is { File: not null } at
                ? Text($"{Root}/gamedata/{file}/{at.Hash}")
            : null;

        public bool Equals(Addresses? other) =>
            other is not null && Root == other.Root && Hashes.SequenceEqual(other.Hashes);

        public override int GetHashCode() => HashCode.Combine(Root, Hashes.Count);
    }
}
