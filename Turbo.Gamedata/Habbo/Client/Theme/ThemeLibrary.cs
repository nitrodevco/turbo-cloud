using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Turbo.Assets;
using Turbo.Assets.Conversion;
using Turbo.Assets.Hab;

namespace Turbo.Gamedata.Habbo.Client.Theme;

/// <summary>A rectangle of a skin, a sheet or a window layout, in pixels.</summary>
internal readonly record struct ThemeRect(double X, double Y, double Width, double Height);

/// <summary>One <c>&lt;window&gt;</c> row of <c>habbo_element_description_xml</c>.</summary>
/// <param name="Skin">The skin that draws it, less <c>_xml</c> (<c>habbo_skin_button_default</c>).</param>
/// <param name="WindowLayout">The window layout that lays it out, less <c>_xml</c>.</param>
/// <param name="Layout">The skin layout it is drawn with (<c>button_black</c>).</param>
internal sealed record ThemeElementRow(
    string Type,
    string Style,
    string? Skin,
    string? WindowLayout,
    string? Layout,
    string? Color
);

/// <summary>A window layout (<c>habbo_window_layout_button</c>): its size and its XML.</summary>
internal sealed record ThemeWindowLayout(
    string Name,
    double Width,
    double Height,
    ThemeXmlNode Root
);

/// <summary>A window placed by a layout, with the tags it carries (<c>_CONTENT</c>, <c>title</c>).</summary>
internal sealed record ThemeLayoutWindow(ThemeXmlNode Node, HashSet<string> Tags);

/// <summary>A skin: its variables, its templates and layouts by name, and its states in order.</summary>
internal sealed record ThemeSkin(
    string Name,
    Dictionary<string, string?> Variables,
    Dictionary<string, ThemeXmlNode> Templates,
    Dictionary<string, ThemeXmlNode> Layouts,
    List<ThemeXmlNode> States
);

/// <summary>
/// The client's window manager library (<c>generated/habbo-window-manager-com.hab</c>) read for
/// the theme, as Studio's <c>server/theme/windowManagerLibrary.ts</c> reads it: its element
/// description (per window type and style, the skin that draws it, the window layout that lays it
/// out and its colour), its window layouts and its skins with the sheets they cut from. An asset is
/// found by its Flash name, which is the <c>.hab</c> entry's own; a bitmap a skin names without its
/// <c>_png</c> is looked up with it too.
/// </summary>
internal sealed partial class ThemeLibrary(HabLibrary hab)
{
    /// <summary>The name JavaScript gives a missing attribute when it becomes a key.</summary>
    private const string UNDEFINED_KEY = "undefined";

    private readonly HashSet<string> _names = [.. hab.Entries.Select(x => x.Name)];
    private readonly Dictionary<string, ThemeWindowLayout?> _layouts = [];
    private readonly Dictionary<string, RgbaImage?> _sheets = [];

    /// <summary>A text asset by its Flash name (<c>habbo_skin_frame_xml</c>); null when there is none.</summary>
    public string? Text(string asset)
    {
        if (!_names.Contains(asset))
            return null;

        var data = hab.GetBinary(asset);

        return data is null ? null : Encoding.UTF8.GetString(data);
    }

    /// <summary>A bitmap asset by its Flash name, with or without its <c>_png</c>; null when there is none.</summary>
    public byte[]? Png(string asset)
    {
        var name = new[] { asset, $"{asset}_png" }.FirstOrDefault(_names.Contains);

        return name is null ? null : hab.GetBinary(name);
    }

    /// <summary>A sheet a skin cuts from, decoded once; null when the library has no such bitmap.</summary>
    public RgbaImage? Sheet(string asset)
    {
        if (_sheets.TryGetValue(asset, out var known))
            return known;

        var data = Png(asset);
        var image = data is null ? null : Decode(data, asset);

        _sheets[asset] = image;

        return image;
    }

    /// <summary>A bitmap decoded, or a <see cref="FormatException"/> naming it.</summary>
    public static RgbaImage Decode(byte[] data, string asset)
    {
        try
        {
            return RgbaImage.Decode(data);
        }
        catch (AssetFormatException ex)
        {
            throw new FormatException(
                $"The window manager library's {asset} is not an image it can read.",
                ex
            );
        }
    }

    /// <summary>Every row of the element description, in order.</summary>
    public List<ThemeElementRow> ElementRows()
    {
        var text =
            Text("habbo_element_description_xml")
            ?? throw new FormatException("The library has no habbo_element_description_xml.");
        var root =
            ThemeXml.Parse(text)
            ?? throw new FormatException(
                "The library's habbo_element_description_xml has no elements."
            );
        var rows = new List<ThemeElementRow>();

        foreach (var node in root.Walk())
        {
            var type = node.Attr("type");
            var style = node.Attr("style");

            if (node.Tag != "window" || string.IsNullOrEmpty(type) || style is null)
                continue;

            rows.Add(
                new ThemeElementRow(
                    type,
                    style,
                    StripXml(node.Attr("asset")),
                    StripXml(node.Attr("window_layout")),
                    node.Attr("layout"),
                    node.Attr("color")
                )
            );
        }

        return rows;
    }

    /// <summary>A window layout by its name (<c>habbo_window_layout_button</c>), read once.</summary>
    public ThemeWindowLayout? WindowLayout(string name)
    {
        if (_layouts.TryGetValue(name, out var known))
            return known;

        var text = Text($"{name}_xml");
        var root = text is null ? null : ThemeXml.Parse(text);
        var layout = root is null
            ? null
            : new ThemeWindowLayout(
                name,
                OrZero(root.Attr("width")),
                OrZero(root.Attr("height")),
                root
            );

        _layouts[name] = layout;

        return layout;
    }

    /// <summary>A skin by its name (<c>habbo_skin_button_default</c>); null when there is none.</summary>
    public ThemeSkin? Skin(string name)
    {
        var text = Text($"{name}_xml");
        var root = text is null ? null : ThemeXml.Parse(text);

        if (root is null)
            return null;

        var variables = new Dictionary<string, string?>();
        var templates = new Dictionary<string, ThemeXmlNode>();
        var layouts = new Dictionary<string, ThemeXmlNode>();

        foreach (var entry in root.Child("variables")?.ChildrenOf("variable") ?? [])
            variables[Key(entry.Attr("key"))] = entry.Attr("value");

        foreach (var entry in root.Child("templates")?.ChildrenOf("template") ?? [])
            templates[Key(entry.Attr("name"))] = entry;

        foreach (var entry in root.Child("layouts")?.ChildrenOf("layout") ?? [])
            layouts[Key(entry.Attr("name"))] = entry;

        return new ThemeSkin(
            name,
            variables,
            templates,
            layouts,
            [.. root.Child("states")?.ChildrenOf("state") ?? []]
        );
    }

    /// <summary>A missing attribute as JavaScript makes it a key.</summary>
    public static string Key(string? value) => value ?? UNDEFINED_KEY;

    /// <summary>
    /// An entity's region (<c>&lt;region&gt;&lt;Rectangle&gt;</c>): each side a number or a
    /// <c>$variable</c> of the skin's own (the scrollbar skins size their buttons that way).
    /// </summary>
    public static ThemeRect? SkinRect(ThemeXmlNode entity, Dictionary<string, string?> variables)
    {
        var rect = entity.Child("region")?.Child("Rectangle");

        if (rect is null)
            return null;

        double Side(string name)
        {
            var value = rect.Attr(name);

            return ThemeNumbers.Parse(
                value is not null && VariableName().IsMatch(value)
                    ? variables.GetValueOrDefault(value[1..])
                    : value
            );
        }

        return new ThemeRect(Side("x"), Side("y"), Side("width"), Side("height"));
    }

    /// <summary>A window's <c>&lt;variables&gt;</c>: a value, or a <c>&lt;Map&gt;</c> (<c>margins</c>) as its own entries.</summary>
    public static Dictionary<string, object> WindowVars(ThemeXmlNode node)
    {
        var vars = new Dictionary<string, object>();

        foreach (var entry in node.Child("variables")?.ChildrenOf("var") ?? [])
        {
            var map = entry.Child("value")?.Child("Map");

            vars[Key(entry.Attr("key"))] = map is null
                ? entry.Attr("value") ?? ""
                : map.ChildrenOf("var")
                    .Select(x => (Key: Key(x.Attr("key")), Value: x.Attr("value")))
                    .GroupBy(x => x.Key)
                    .ToDictionary(x => x.Key, x => x.Last().Value);
        }

        return vars;
    }

    /// <summary>The windows of a layout, depth first, with their tags.</summary>
    public static List<ThemeLayoutWindow> LayoutWindows(ThemeWindowLayout layout)
    {
        var found = new List<ThemeLayoutWindow>();

        void Visit(ThemeXmlNode node)
        {
            foreach (var entry in node.Children)
            {
                if (entry.Tag is "variables" or "params" or "filters" or "scale" or "children")
                {
                    if (entry.Tag == "children")
                        Visit(entry);

                    continue;
                }

                found.Add(
                    new ThemeLayoutWindow(
                        entry,
                        [
                            .. (entry.Attr("tags") ?? "")
                                .Split(',')
                                .Select(x => x.Trim())
                                .Where(x => x.Length > 0),
                        ]
                    )
                );
                Visit(entry);
            }
        }

        Visit(layout.Root.Child("window") ?? layout.Root);

        return found;
    }

    /// <summary>The layout's own <c>&lt;DropShadowFilter&gt;</c>, as the theme's <c>DropShadowConfig</c>.</summary>
    public static JsonObject? LayoutDropShadow(ThemeWindowLayout layout)
    {
        var filter = layout.Root.Child("filters")?.Child("DropShadowFilter");

        if (filter is null)
            return null;

        JsonNode? Number(string name, double fallback)
        {
            var value = filter.Attr(name);

            return ThemeNumbers.Json(value is null ? fallback : ThemeNumbers.Parse(value));
        }

        return new JsonObject
        {
            ["distance"] = Number("distance", 4),
            ["angle"] = Number("angle", 45),
            ["color"] = ThemeXml.HexColor(filter.Attr("color") ?? "0") ?? "#000000",
            ["alpha"] = Number("alpha", 1),
            ["blur"] = Number("blurX", 4),
        };
    }

    /// <summary><c>Number(value) || 0</c>: a size that is missing or not a number is 0.</summary>
    private static double OrZero(string? value)
    {
        var number = ThemeNumbers.Parse(value);

        return double.IsNaN(number) ? 0 : number;
    }

    private static string? StripXml(string? asset) =>
        asset is null ? null : XmlSuffix().Replace(asset, "");

    [GeneratedRegex(@"_xml\z")]
    private static partial Regex XmlSuffix();

    [GeneratedRegex(@"^\$[A-Za-z0-9_]+\z")]
    private static partial Regex VariableName();
}
