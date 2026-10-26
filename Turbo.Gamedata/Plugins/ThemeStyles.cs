using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Turbo.Gamedata.Variables;
using Turbo.Primitives.Plugins;

namespace Turbo.Gamedata.Plugins;

/// <summary>
/// A theme's style properties (<c>docs/plugin-data.md</c>): the named, typed values its window
/// patches read as <c>${style:name}</c>, declared with their defaults in <c>theme.json</c> beside its
/// manifest. Clients are given each as the patch needs it (<see cref="ClientValue"/>) in the merged
/// variable <c>templates.styles</c>, under <c>&lt;plugin key&gt;:&lt;name&gt;</c>; the hotel's own
/// entries there are staff's changes, over the theme's defaults and kept across its updates.
/// </summary>
internal static class ThemeStyles
{
    /// <summary>The file in a plugin's folder that declares its style properties.</summary>
    public const string FILE = "theme.json";

    private const int TEXT_MAX_LENGTH = 200;

    private static readonly JsonDocumentOptions READ = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// The key of the base theme Turbo ships: Habbo's own look as style properties (its
    /// <c>theme.json</c> is <c>Plugins/Themes/habbo/theme.json</c> here), each the client's own until
    /// staff set it, that a theme names in <c>"extends"</c> and the hotel's own plugin extends.
    /// </summary>
    public const string BASE = "habbo";

    private const string HOTEL = "hotel";
    private const string BASE_RESOURCE = "Turbo.Gamedata.Themes.habbo.theme.json";
    private const int EXTENDS_MAX_DEPTH = 8;

    // Marks, in a merged declaration, the theme whose default it has; no file's own field.
    private const string FROM = "$from";

    /// <summary>
    /// The properties the plugin <paramref name="pluginKey"/>'s <c>theme.json</c> in
    /// <paramref name="folder"/> declares: those of the theme it <c>"extends"</c> first, in that
    /// theme's order, each its own field by field where the file names it again (a redeclaration
    /// with only a <c>default</c> keeps the parent's type, target and label), then its new ones. The
    /// hotel's own plugin with no file extends <see cref="BASE"/>; another plugin with no file
    /// declares none. <paramref name="folderOf"/> finds the folder of a parent that is a plugin.
    /// Throws <see cref="ArgumentException"/> naming what is wrong in a file that is there, or a
    /// parent that can't be found.
    /// </summary>
    public static ImmutableArray<ThemeStyleProperty> Read(
        string pluginKey,
        string? folder,
        Func<string, string?>? folderOf = null
    )
    {
        var declared = Declarations(pluginKey, folder, folderOf, []);
        var list = ImmutableArray.CreateBuilder<ThemeStyleProperty>(declared.Count);

        foreach (var (name, entry) in declared)
        {
            var type = Parse(entry["type"]?.GetValue<string>(), name);
            var property = new ThemeStyleProperty(
                name,
                type,
                entry["default"] switch
                {
                    null => null,
                    // A position may be written as clients get it.
                    JsonObject position when type == ThemeStyleType.Position => FromClient(
                        new ThemeStyleProperty(
                            name,
                            type,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null
                        ),
                        position
                    ),
                    var raw => Display(raw.ToJsonString()),
                },
                entry["group"]?.GetValue<string>(),
                entry["label"]?.GetValue<string>(),
                entry["hint"]?.GetValue<string>(),
                entry["overlay"]?.GetValue<string>(),
                entry["min"]?.GetValue<double>(),
                entry["max"]?.GetValue<double>(),
                VariableOf(entry["variable"], name),
                entry[FROM]?.GetValue<string>()
            );

            // A default that would be refused as a change is a mistake of the theme's.
            if (property.Default is { } value)
                Check(property, value);

            list.Add(property);
        }

        return list.ToImmutable();
    }

    /// <summary>
    /// The plugin <paramref name="pluginKey"/> as a theme: the theme its <c>theme.json</c> in
    /// <paramref name="folder"/> extends; the hotel's own plugin with no file extends
    /// <see cref="BASE"/>; null for another plugin with no file.
    /// </summary>
    public static PluginTheme? ThemeOf(string pluginKey, string? folder) =>
        ReadFile(folder) switch
        {
            null when string.Equals(pluginKey, HOTEL, StringComparison.Ordinal) => new(
                pluginKey,
                BASE
            ),
            null => null,
            var root => new(pluginKey, root["extends"]?.GetValue<string>()),
        };

    /// <summary>The properties of a theme's file and of the ones it extends, as their JSON, merged in order.</summary>
    private static List<KeyValuePair<string, JsonObject>> Declarations(
        string key,
        string? folder,
        Func<string, string?>? folderOf,
        List<string> seen
    )
    {
        if (seen.Contains(key, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"{FILE} extends in a circle: {string.Join(" -> ", seen.Append(key))}.",
                nameof(folder)
            );

        if (seen.Count >= EXTENDS_MAX_DEPTH)
            throw new ArgumentException(
                $"{FILE} extends more than {EXTENDS_MAX_DEPTH} themes deep.",
                nameof(folder)
            );

        seen.Add(key);

        // The base theme as a parent is the one shipped here, whatever a folder of that name holds.
        var root =
            seen.Count > 1 && string.Equals(key, BASE, StringComparison.OrdinalIgnoreCase)
                ? BaseTheme()
                : ReadFile(folder);

        var parent = root is null
            ? (string.Equals(key, HOTEL, StringComparison.Ordinal) ? BASE : null)
            : root["extends"]?.GetValue<string>();

        var merged =
            parent is null ? []
            : string.Equals(parent, BASE, StringComparison.OrdinalIgnoreCase)
                ? Declarations(BASE, null, folderOf, seen)
            : Declarations(
                parent,
                folderOf?.Invoke(parent)
                    ?? throw new ArgumentException(
                        $"{FILE} extends {parent}, which is not installed.",
                        nameof(folder)
                    ),
                folderOf,
                seen
            );

        // A theme that only extends one changes nothing of it.
        if (root is null || (parent is not null && root["styles"] is null))
            return merged;

        if (root["styles"] is not JsonObject styles)
            throw new ArgumentException(
                $"{FILE} is {{ \"styles\": {{ \"<name>\": {{ \"type\": \"color\", \"default\": \"#6b6861\" }} }} }}.",
                nameof(folder)
            );

        foreach (var (name, node) in styles)
        {
            if (node is not JsonObject entry)
                throw new ArgumentException($"The style {name} is an object.", nameof(folder));

            var at = merged.FindIndex(x => x.Key == name);

            if (at < 0)
            {
                var added = (JsonObject)entry.DeepClone();

                added[FROM] = key;
                merged.Add(new(name, added));

                continue;
            }

            var over = (JsonObject)merged[at].Value.DeepClone();

            foreach (var (field, value) in entry)
                over[field] = value?.DeepClone();

            if (entry.ContainsKey("default"))
                over[FROM] = key;

            merged[at] = new(name, over);
        }

        return merged;
    }

    private static JsonNode? ReadFile(string? folder)
    {
        var path = folder is null ? null : Path.Combine(folder, FILE);

        if (path is null || !File.Exists(path))
            return null;

        try
        {
            return JsonNode.Parse(File.ReadAllText(path), documentOptions: READ);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"{FILE} isn't JSON. {ex.Message}", nameof(folder), ex);
        }
    }

    /// <summary>The base theme's <c>theme.json</c>, shipped in this assembly.</summary>
    private static JsonNode BaseTheme()
    {
        using var stream =
            typeof(ThemeStyles).Assembly.GetManifestResourceStream(BASE_RESOURCE)
            ?? throw new InvalidOperationException($"{BASE_RESOURCE} is not in the build.");

        return JsonNode.Parse(stream, documentOptions: READ)!;
    }

    /// <summary>
    /// A property's <c>"variable"</c>: the merged variable its value is also an entry of, then the
    /// path in it (<c>["frame.overrides", "catalog", "color"]</c>).
    /// </summary>
    private static ImmutableArray<string> VariableOf(JsonNode? node, string name)
    {
        if (node is null)
            return [];

        if (
            node is not JsonArray array
            || array.Count < 2
            || !array.All(x =>
                x is JsonValue value && value.TryGetValue<string>(out var part) && part.Length > 0
            )
        )
            throw new ArgumentException(
                $"The style {name}'s variable is [ \"<variable>\", \"<entry>\", ... ].",
                nameof(node)
            );

        ImmutableArray<string> path = [.. array.Select(x => x!.GetValue<string>())];

        if (!ExternalVariablesFile.IsMerged(path[0]))
            throw new ArgumentException(
                $"The style {name}'s variable {path[0]} is none of those themes add entries to: {string.Join(", ", ExternalVariablesFile.MERGED)}.",
                nameof(node)
            );

        return path;
    }

    /// <summary>
    /// Every place besides <c>templates.styles</c> a property's value goes, each a merged variable
    /// and the path in it: its overlay's <c>asset.overlays</c> entry, then its <c>"variable"</c>.
    /// </summary>
    public static IEnumerable<ImmutableArray<string>> TargetsOf(ThemeStyleProperty property)
    {
        if (property is { Type: ThemeStyleType.Image, Overlay: { } asset })
            yield return [ExternalVariablesFile.ASSET_OVERLAYS, asset];

        if (!property.Variable.IsDefaultOrEmpty)
            yield return property.Variable;
    }

    /// <summary>
    /// A client value (<see cref="ClientValue"/>) as a variable's entry takes it: a colour as its
    /// <c>#rrggbb</c> text, which <c>frame.overrides</c> and <c>loading.screen</c> both read; anything
    /// else as it is.
    /// </summary>
    public static JsonNode TargetValue(ThemeStyleProperty property, JsonNode client) =>
        property.Type == ThemeStyleType.Color
            ? JsonValue.Create(FromClient(property, client))
            : client.DeepClone();

    /// <summary>
    /// Sets a target's path (<see cref="TargetsOf"/>, after its variable) in that variable's
    /// entries; with <paramref name="keep"/> a value already there stays.
    /// </summary>
    public static void SetPath(
        Dictionary<string, JsonNode?> entries,
        ImmutableArray<string> path,
        JsonNode value,
        bool keep = false
    )
    {
        if (path.Length == 2)
        {
            if (!keep || !entries.ContainsKey(path[1]))
                entries[path[1]] = value;

            return;
        }

        if (entries.GetValueOrDefault(path[1]) is not JsonObject node)
            entries[path[1]] = node = [];

        for (var i = 2; i < path.Length - 1; i++)
        {
            if (node[path[i]] is not JsonObject next)
                node[path[i]] = next = [];

            node = next;
        }

        if (!keep || !node.ContainsKey(path[^1]))
            node[path[^1]] = value;
    }

    /// <summary>
    /// Takes a target's path away from its variable's entries, and each object that leaves empty;
    /// false when it wasn't there.
    /// </summary>
    public static bool RemovePath(
        Dictionary<string, JsonNode?> entries,
        ImmutableArray<string> path
    )
    {
        if (path.Length == 2)
            return entries.Remove(path[1]);

        if (entries.GetValueOrDefault(path[1]) is not JsonObject top)
            return false;

        // chain[i] is chain[i - 1][path[i + 1]].
        var chain = new List<JsonObject> { top };

        for (var i = 2; i < path.Length - 1; i++)
        {
            if (chain[^1][path[i]] is not JsonObject next)
                return false;

            chain.Add(next);
        }

        if (!chain[^1].Remove(path[^1]))
            return false;

        for (var i = chain.Count - 1; i > 0 && chain[i].Count == 0; i--)
            chain[i - 1].Remove(path[i + 1]);

        if (top.Count == 0)
            entries.Remove(path[1]);

        return true;
    }

    /// <summary>
    /// <paramref name="value"/> as clients are given it: a colour (<c>#rgb</c>, <c>#rrggbb</c>,
    /// <c>#aarrggbb</c>) the <c>0xAARRGGBB</c> number a patch's <c>color</c> is, a number a number,
    /// an image its address (<paramref name="address"/> for a path in the plugin's <c>wwwroot/</c>),
    /// a position (<c>x,y</c>) <c>{ "x": x, "y": y }</c>, a font or text the text. Throws <see cref="ArgumentException"/> for one the type refuses.
    /// </summary>
    public static JsonNode ClientValue(
        ThemeStyleProperty property,
        string value,
        Func<string, string?> address
    )
    {
        Check(property, value);

        return property.Type switch
        {
            ThemeStyleType.Color => JsonValue.Create(Argb(value)),
            ThemeStyleType.Number => JsonValue.Create(
                double.Parse(value, CultureInfo.InvariantCulture)
            ),
            ThemeStyleType.Position => new JsonObject
            {
                ["x"] = PositionOf(value).X,
                ["y"] = PositionOf(value).Y,
            },
            ThemeStyleType.Image when IsUrl(value) => JsonValue.Create(value),
            ThemeStyleType.Image => JsonValue.Create(
                address(value)
                    ?? throw new ArgumentException(
                        $"The image {value} of the style {property.Name} is no file of the plugin's wwwroot.",
                        nameof(value)
                    )
            ),
            _ => JsonValue.Create(value),
        };
    }

    /// <summary>A client value (<see cref="ClientValue"/>) as staff read and type it: a colour <c>#rrggbb</c> (<c>#aarrggbb</c> when not opaque).</summary>
    public static string FromClient(ThemeStyleProperty property, JsonNode? node)
    {
        if (node is null)
            return string.Empty;

        if (
            property.Type == ThemeStyleType.Color
            && node.GetValueKind() == JsonValueKind.Number
            && double.TryParse(
                node.ToJsonString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var argb
            )
        )
        {
            var color = (uint)argb;

            return color >> 24 == 0xFF ? $"#{color & 0xFFFFFF:x6}" : $"#{color:x8}";
        }

        if (
            property.Type == ThemeStyleType.Position
            && node is JsonObject position
            && position["x"] is JsonValue x
            && position["y"] is JsonValue y
        )
            return $"{x.ToJsonString()},{y.ToJsonString()}";

        return Display(node.ToJsonString());
    }

    /// <summary>Throws <see cref="ArgumentException"/> when the property's type refuses <paramref name="value"/>.</summary>
    public static void Check(ThemeStyleProperty property, string value)
    {
        var problem = property.Type switch
        {
            ThemeStyleType.Color when !TryArgb(value, out _) =>
                "is a colour: #rrggbb, or #aarrggbb with its alpha",
            ThemeStyleType.Number
                when !double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var number
                ) => "is a number",
            ThemeStyleType.Number
                when (
                    property.Min is { } min
                    && double.Parse(value, CultureInfo.InvariantCulture) < min
                )
                    || (
                        property.Max is { } max
                        && double.Parse(value, CultureInfo.InvariantCulture) > max
                    ) =>
                $"is from {property.Min?.ToString(CultureInfo.InvariantCulture) ?? "any"} to {property.Max?.ToString(CultureInfo.InvariantCulture) ?? "any"}",
            ThemeStyleType.Position when !TryPosition(value, out _) =>
                "is where the window opens: x,y (84,153)",
            ThemeStyleType.Image when value.Length == 0 => "names an image",
            ThemeStyleType.Image when value.StartsWith("//", StringComparison.Ordinal) =>
                "is a path in the plugin's wwwroot, /a/path on the hotel or https://...",
            _ when value.Length > TEXT_MAX_LENGTH => $"is at most {TEXT_MAX_LENGTH} characters",
            _ => null,
        };

        if (problem is not null)
            throw new ArgumentException($"The style {property.Name} {problem}.", nameof(value));
    }

    private static ThemeStyleType Parse(string? type, string name) =>
        type switch
        {
            "color" => ThemeStyleType.Color,
            "number" => ThemeStyleType.Number,
            "image" => ThemeStyleType.Image,
            "font" => ThemeStyleType.Font,
            "text" => ThemeStyleType.Text,
            "position" => ThemeStyleType.Position,
            _ => throw new ArgumentException(
                $"The style {name} has the type \"{type}\": color, number, image, font, text or position."
            ),
        };

    /// <summary>A JSON value as text: a string's own characters, anything else its JSON.</summary>
    private static string Display(string json)
    {
        var node = JsonNode.Parse(json);

        return node is JsonValue value && value.TryGetValue<string>(out var text) ? text : json;
    }

    /// <summary>Whether <paramref name="value"/> is an address: <c>/a/path</c> on the hotel, or <c>http(s)://...</c>.</summary>
    internal static bool IsUrl(string value) =>
        !value.StartsWith("//", StringComparison.Ordinal) && value.StartsWith('/')
        || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

    private static (int X, int Y) PositionOf(string value) =>
        TryPosition(value, out var position)
            ? position
            : throw new ArgumentException(value, nameof(value));

    private static bool TryPosition(string value, out (int X, int Y) position)
    {
        position = default;

        var parts = value.Split(',', StringSplitOptions.TrimEntries);

        if (
            parts.Length != 2
            || !int.TryParse(
                parts[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var x
            )
            || !int.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var y
            )
        )
            return false;

        position = (x, y);

        return true;
    }

    private static uint Argb(string value) =>
        TryArgb(value, out var argb) ? argb : throw new ArgumentException(value, nameof(value));

    private static bool TryArgb(string value, out uint argb)
    {
        argb = 0;

        var hex = value.Trim().TrimStart('#');

        if (hex.Length == 3)
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);

        if (
            !value.TrimStart().StartsWith('#')
            || hex.Length is not (6 or 8)
            || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out argb)
        )
            return false;

        if (hex.Length == 6)
            argb |= 0xFF000000;

        return true;
    }

    /// <summary>The entry key of a plugin's style in <c>templates.styles</c>.</summary>
    public static string EntryOf(string pluginKey, string name) => $"{pluginKey}:{name}";

    /// <summary>The entries of a merged variable's JSON object value (it may be the object as JSON text).</summary>
    public static Dictionary<string, JsonNode?> Entries(string? json)
    {
        var entries = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(json))
            return entries;

        try
        {
            var node = JsonNode.Parse(json);

            if (node is JsonValue text && text.TryGetValue<string>(out var inner))
                node = JsonNode.Parse(inner);

            if (node is JsonObject obj)
                foreach (var (key, value) in obj)
                    entries[key] = value?.DeepClone();
        }
        catch (JsonException) { }

        return entries;
    }
}
