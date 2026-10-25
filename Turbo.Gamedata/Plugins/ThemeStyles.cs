using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    /// The properties <c>theme.json</c> in <paramref name="folder"/> declares, in its order: none
    /// without the file. Throws <see cref="ArgumentException"/> naming what is wrong in one that is
    /// there.
    /// </summary>
    public static ImmutableArray<ThemeStyleProperty> Read(string folder)
    {
        var path = Path.Combine(folder, FILE);

        if (!File.Exists(path))
            return [];

        JsonNode? root;

        try
        {
            root = JsonNode.Parse(File.ReadAllText(path), documentOptions: READ);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"{FILE} isn't JSON. {ex.Message}", nameof(folder), ex);
        }

        if (root?["styles"] is not JsonObject styles)
            throw new ArgumentException(
                $"{FILE} is {{ \"styles\": {{ \"<name>\": {{ \"type\": \"color\", \"default\": \"#6b6861\" }} }} }}.",
                nameof(folder)
            );

        var list = ImmutableArray.CreateBuilder<ThemeStyleProperty>(styles.Count);

        foreach (var (name, node) in styles)
        {
            if (node is not JsonObject entry)
                throw new ArgumentException($"The style {name} is an object.", nameof(folder));

            var type = Parse(entry["type"]?.GetValue<string>(), name);
            var property = new ThemeStyleProperty(
                name,
                type,
                entry["default"]?.ToJsonString() is { } raw ? Display(raw) : string.Empty,
                entry["group"]?.GetValue<string>(),
                entry["label"]?.GetValue<string>(),
                entry["hint"]?.GetValue<string>(),
                entry["overlay"]?.GetValue<string>(),
                entry["min"]?.GetValue<double>(),
                entry["max"]?.GetValue<double>()
            );

            // A default that would be refused as a change is a mistake of the theme's.
            Check(property, property.Default);
            list.Add(property);
        }

        return list.ToImmutable();
    }

    /// <summary>
    /// <paramref name="value"/> as clients are given it: a colour (<c>#rgb</c>, <c>#rrggbb</c>,
    /// <c>#aarrggbb</c>) the <c>0xAARRGGBB</c> number a patch's <c>color</c> is, a number a number,
    /// an image its address (<paramref name="address"/> for a path in the plugin's <c>wwwroot/</c>),
    /// a font or text the text. Throws <see cref="ArgumentException"/> for one the type refuses.
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
            _ => throw new ArgumentException(
                $"The style {name} has the type \"{type}\": color, number, image, font or text."
            ),
        };

    /// <summary>A JSON value as text: a string's own characters, anything else its JSON.</summary>
    private static string Display(string json)
    {
        var node = JsonNode.Parse(json);

        return node is JsonValue value && value.TryGetValue<string>(out var text) ? text : json;
    }

    private static bool IsUrl(string value) =>
        value.StartsWith('/')
        || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

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
