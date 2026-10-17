using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Turbo.Assets.Conversion;

namespace Turbo.Gamedata.Habbo.Client.Theme;

/// <summary>
/// The theme's variants (<c>theme-variants.json</c>), built from the client's window manager
/// library and the port's rules (<see cref="ThemeRules"/>), as Studio's
/// <c>server/theme/themeVariants.ts</c> builds them. A variant is a theme component's style
/// (<c>frame</c> style 3, <c>button</c> style 0). What the client says of one is read off its row in
/// the element description and the window layout the row names, the way <c>WindowManager</c>
/// builds the window: its colour is the variant's <c>tintColor</c>, the layout's size its
/// <c>layout.minWidth</c> / <c>minHeight</c>, the layout's caption its <c>textStyle</c>,
/// <c>textColor</c> and padding, the layout's drop shadow its <c>dropShadow</c>, and the themed
/// windows the layout places what it cascades to its children. The rules hold the rest, per
/// component and style; a variant the rules do not name is not in the theme.
/// </summary>
internal static partial class ThemeVariants
{
    /// <summary>The icon set's sheet in the bundle: <c>habbo_icons_png</c>, texture key <c>icon-set-src</c>.</summary>
    public const string ICON_SET_ASSET = "theme-icon-set";

    /// <summary>The Flash window type each theme component (by its cascade key) draws.</summary>
    private static readonly Dictionary<string, string> COMPONENT_ELEMENTS = new()
    {
        ["border"] = "border",
        ["bubble"] = "bubble",
        ["bubblePointerDown"] = "bubble_pointer_down",
        ["bubblePointerLeft"] = "bubble_pointer_left",
        ["bubblePointerRight"] = "bubble_pointer_right",
        ["bubblePointerUp"] = "bubble_pointer_up",
        ["button"] = "button",
        ["buttonGroupCenter"] = "button_group_center",
        ["buttonGroupLeft"] = "button_group_left",
        ["buttonGroupRight"] = "button_group_right",
        ["buttonThick"] = "button_thick",
        ["checkBox"] = "checkbox",
        ["closeButton"] = "closebutton",
        ["containerButton"] = "container_button",
        ["droplist"] = "droplist",
        ["droplistItem"] = "droplist_item",
        ["dropmenu"] = "dropmenu",
        ["dropmenuItem"] = "dropmenu_item",
        ["frame"] = "frame",
        ["framePointerDown"] = "frame_pointer_down",
        ["header"] = "header",
        ["iconButton"] = "iconbutton",
        ["radioButton"] = "radiobutton",
        ["region"] = "region",
        ["scaler"] = "scaler",
        ["scrollableItemGridVertical"] = "scrollable_itemgrid_vertical",
        ["scrollableItemListVertical"] = "scrollable_itemlist_vertical",
        ["scrollbarSliderBarHorizontal"] = "scrollbar_slider_bar_horizontal",
        ["scrollbarSliderBarVertical"] = "scrollbar_slider_bar_vertical",
        ["scrollbarSliderButtonDown"] = "scrollbar_slider_button_down",
        ["scrollbarSliderButtonLeft"] = "scrollbar_slider_button_left",
        ["scrollbarSliderButtonRight"] = "scrollbar_slider_button_right",
        ["scrollbarSliderButtonUp"] = "scrollbar_slider_button_up",
        ["scrollbarSliderTrackHorizontal"] = "scrollbar_slider_track_horizontal",
        ["scrollbarSliderTrackVertical"] = "scrollbar_slider_track_vertical",
        ["tabButton"] = "tab_button",
        ["tabContent"] = "tab_content",
        ["tabContext"] = "tab_context",
        ["tooltip"] = "tooltip",
    };

    private static readonly Dictionary<string, string> ELEMENT_COMPONENTS = COMPONENT_ELEMENTS
        .GroupBy(x => x.Value)
        .ToDictionary(x => x.Key, x => x.Last().Key);

    /// <summary>The header variant a frame's own title bar is: <c>frame-&lt;style&gt;</c>.</summary>
    private const string TITLEBAR_PREFIX = "frame-";

    /// <summary>
    /// <c>theme-variants.json</c> (less its icon set) and the sprites it names: every variant the
    /// rules name, built over what the client's files say of it, and the title bar of every frame
    /// that draws its own.
    /// </summary>
    public static (JsonObject File, Dictionary<string, RgbaImage> Sprites) Build(
        ThemeLibrary library,
        JsonObject rules
    )
    {
        var rows = library.ElementRows();
        var variants = new JsonObject();
        var cascades = new JsonObject();
        var sprites = new Dictionary<string, RgbaImage>();
        var components = rules["components"] as JsonObject ?? [];

        foreach (var key in JsKeys(components))
        {
            var component = components[key] as JsonObject ?? [];
            var element = component.TryGetPropertyValue("element", out var named)
                ? (string?)named
                : COMPONENT_ELEMENTS.GetValueOrDefault(key);
            var described = RowsByStyle(rows, element);
            var componentVariants = component["variants"] as JsonObject ?? [];
            var componentCascade = component["cascade"] as JsonObject;
            var target = ThemeJson.Ensure(variants, key);

            foreach (var style in JsKeys(componentVariants))
            {
                // A frame's title bar: built with the frames, below.
                if (
                    key == "header"
                    && style.StartsWith(TITLEBAR_PREFIX, System.StringComparison.Ordinal)
                )
                    continue;

                var row = described.GetValueOrDefault(style);

                target[style] = ApplyRule(
                    row is null ? [] : DeriveAll(library, key, row, sprites),
                    componentVariants[style] as JsonObject
                );
            }

            // The cascade: derived for every described style, the rule's over it; kept where it names anything.
            var styles = described
                .Keys.Concat(componentCascade is null ? [] : JsKeys(componentCascade))
                .Distinct();

            foreach (var style in styles)
            {
                var row = described.GetValueOrDefault(style);
                var rule = componentCascade?[style] as JsonObject;
                var cascade = row is null ? [] : DeriveCascade(library, row);

                ThemeJson.Assign(cascade, rule?["set"] as JsonObject);
                Unset(cascade, rule);

                if (cascade.Count > 0)
                    ThemeJson.Ensure(cascades, key)[style] = cascade;
            }
        }

        var frameRule = components["frame"] as JsonObject;
        var headerRule = components["header"] as JsonObject;

        // The frames that draw their own title bar cascade to it, and it to its close button.
        foreach (var (style, row) in RowsByStyle(rows, COMPONENT_ELEMENTS["frame"]))
        {
            var titlebar = Truthy((frameRule?["variants"] as JsonObject)?[style])
                ? DeriveTitlebar(library, row)
                : null;

            if (titlebar is null)
                continue;

            var key = TITLEBAR_PREFIX + style;
            var rule = (headerRule?["cascade"] as JsonObject)?[key] as JsonObject;
            var cascade = titlebar.Value.Cascade;

            ThemeJson.Assign(cascade, rule?["set"] as JsonObject);
            Unset(cascade, rule);

            ThemeJson.Ensure(variants, "header")[key] = ApplyRule(
                titlebar.Value.Variant,
                (headerRule?["variants"] as JsonObject)?[key] as JsonObject
            );

            if (cascade.Count > 0)
                ThemeJson.Ensure(cascades, "header")[key] = cascade;

            var frameCascadeSet =
                ((frameRule?["cascade"] as JsonObject)?[style] as JsonObject)?["set"] as JsonObject;

            if (frameCascadeSet?["header"] is null || !Truthy(frameCascadeSet["header"]))
                ThemeJson.Ensure(ThemeJson.Ensure(cascades, "frame"), style)["header"] = key;
        }

        return (
            new JsonObject
            {
                ["version"] = 1,
                ["variants"] = variants,
                ["cascade"] = cascades,
            },
            sprites
        );
    }

    /// <summary>
    /// <c>habbo_skin_icon_set_xml</c>: each <c>&lt;template name="icon_N"&gt;</c> is one rect of
    /// its sheet (<c>habbo_icons_png</c>), and a layout's <c>&lt;icon style="N"&gt;</c> picks it.
    /// The sheet as it is, and the rects by style; null when the library has none.
    /// </summary>
    public static (RgbaImage Image, JsonObject Icons)? IconSet(ThemeLibrary library)
    {
        var root = ThemeXml.Parse(library.Text("habbo_skin_icon_set_xml") ?? "");
        var variables = new Dictionary<string, string?>();

        foreach (var variable in root?.Child("variables")?.ChildrenOf("variable") ?? [])
            variables["$" + ThemeLibrary.Key(variable.Attr("key"))] = variable.Attr("value");

        var icons = new JsonObject();
        string? sheet = null;

        foreach (var template in root?.Child("templates")?.ChildrenOf("template") ?? [])
        {
            var style = IconName().Match(template.Attr("name") ?? "");
            var rect = template
                .Child("entities")
                ?.Child("entity")
                ?.Child("region")
                ?.Child("Rectangle");

            if (!style.Success || rect is null)
                continue;

            sheet ??=
                variables.GetValueOrDefault(template.Attr("asset") ?? "") ?? template.Attr("asset");
            icons[style.Groups[1].Value] = RectOf(rect);
        }

        var data = string.IsNullOrEmpty(sheet) ? null : library.Png(sheet);

        return data is not null && icons.Count > 0
            ? (ThemeLibrary.Decode(data, sheet!), icons)
            : null;
    }

    /// <summary>Everything the client's files say of one variant: its row and window layout's fields, then its skin's art.</summary>
    private static JsonObject DeriveAll(
        ThemeLibrary library,
        string key,
        ThemeElementRow row,
        Dictionary<string, RgbaImage> sprites
    )
    {
        var art = SkinArt.DeriveArt(
            library,
            key,
            row,
            ComponentAdapter.BY_COMPONENT.GetValueOrDefault(key)
        );

        foreach (var (asset, image) in art?.Sprites ?? [])
            sprites[asset] = image;

        var variant = DeriveVariant(library, row);

        ThemeJson.Assign(variant, art?.Fields);

        return variant;
    }

    /// <summary>What the client's files say of one variant: the fields its row and window layout give it.</summary>
    private static JsonObject DeriveVariant(ThemeLibrary library, ThemeElementRow row)
    {
        var variant = new JsonObject();
        var tint = ThemeXml.HexColor(row.Color);

        if (tint is not null)
            variant["tintColor"] = tint;

        var layout = row.WindowLayout is null ? null : library.WindowLayout(row.WindowLayout);

        if (layout is null)
            return variant;

        var box = new JsonObject
        {
            ["minWidth"] = ThemeNumbers.Json(layout.Width),
            ["minHeight"] = ThemeNumbers.Json(layout.Height),
        };
        var caption = CaptionOf(layout);

        if (caption is not null)
        {
            var vars = ThemeLibrary.WindowVars(caption);

            if (vars.GetValueOrDefault("text_style") is string { Length: > 0 } textStyle)
                variant["textStyle"] = textStyle;

            if (
                vars.GetValueOrDefault("text_color") is string { Length: > 0 } textColor
                && ThemeXml.HexColor(textColor) is { } color
            )
                variant["textColor"] = color;

            if (vars.GetValueOrDefault("margins") is Dictionary<string, string?> margins)
            {
                box["paddingLeft"] = Margin(margins, "left");
                box["paddingTop"] = Margin(margins, "top");
                box["paddingRight"] = Margin(margins, "right");
                box["paddingBottom"] = Margin(margins, "bottom");
            }
        }

        variant["layout"] = box;

        if (ThemeLibrary.LayoutDropShadow(layout) is { } shadow)
            variant["dropShadow"] = shadow;

        return variant;
    }

    /// <summary><c>Number(margins.side ?? 0)</c>.</summary>
    private static JsonNode? Margin(Dictionary<string, string?> margins, string side) =>
        ThemeNumbers.Json(
            margins.GetValueOrDefault(side) is { } value ? ThemeNumbers.Parse(value) : 0
        );

    /// <summary>The window a layout draws its caption with: the one tagged <c>title</c>, else its only text.</summary>
    private static ThemeXmlNode? CaptionOf(ThemeWindowLayout layout)
    {
        var windows = ThemeLibrary
            .LayoutWindows(layout)
            .Where(x => x.Node.Tag is "label" or "text")
            .ToList();

        return (
            windows.FirstOrDefault(x => x.Tags.Contains("title"))
            ?? (windows.Count == 1 ? windows[0] : null)
        )?.Node;
    }

    /// <summary>What a variant cascades to its children: each themed window its layout places, at its own style or the row's.</summary>
    private static JsonObject DeriveCascade(ThemeLibrary library, ThemeElementRow row)
    {
        var layout = row.WindowLayout is null ? null : library.WindowLayout(row.WindowLayout);
        var cascade = new JsonObject();

        foreach (var window in layout is null ? [] : ThemeLibrary.LayoutWindows(layout))
        {
            if (
                ELEMENT_COMPONENTS.TryGetValue(window.Node.Tag, out var key)
                && !cascade.ContainsKey(key)
            )
                cascade[key] = window.Node.Attr("style") ?? row.Style;
        }

        return cascade;
    }

    private static HashSet<string> ParamsOf(ThemeXmlNode node) =>
        [
            .. (node.Child("params")?.ChildrenOf("param") ?? []).Select(x =>
                ThemeLibrary.Key(x.Attr("name"))
            ),
        ];

    private static ThemeRect WindowRect(ThemeXmlNode node) =>
        new(
            ThemeNumbers.Parse(node.Attr("x") ?? "0"),
            ThemeNumbers.Parse(node.Attr("y") ?? "0"),
            ThemeNumbers.Parse(node.Attr("width") ?? "0"),
            ThemeNumbers.Parse(node.Attr("height") ?? "0")
        );

    private static JsonObject RectOf(ThemeXmlNode node)
    {
        var rect = WindowRect(node);

        return new JsonObject
        {
            ["x"] = ThemeNumbers.Json(rect.X),
            ["y"] = ThemeNumbers.Json(rect.Y),
            ["width"] = ThemeNumbers.Json(rect.Width),
            ["height"] = ThemeNumbers.Json(rect.Height),
        };
    }

    /// <summary>
    /// The title bar of a frame whose window layout has no <c>header</c> window but places
    /// <c>header_title_text</c> and <c>header_button_close</c> itself (<c>illumina_light_frame</c>):
    /// a header variant with no art (the frame skin is the bar), the title and close button where the
    /// layout pins them, and the bar as tall as its <c>titlebar</c> container.
    /// </summary>
    private static (JsonObject Variant, JsonObject Cascade)? DeriveTitlebar(
        ThemeLibrary library,
        ThemeElementRow row
    )
    {
        var layout = row.WindowLayout is null ? null : library.WindowLayout(row.WindowLayout);
        var windows = layout is null
            ? []
            : ThemeLibrary.LayoutWindows(layout).Select(x => x.Node).ToList();
        var title = windows.FirstOrDefault(x => x.Attr("name") == "header_title_text");

        if (layout is null || title is null || windows.Any(x => x.Tag == "header"))
            return null;

        var bar = windows.FirstOrDefault(x => x.Attr("name") == "titlebar");
        var close = windows.FirstOrDefault(x => x.Attr("name") == "header_button_close");
        var menu = windows.FirstOrDefault(x => x.Attr("name") == "header_button_menu");
        var vars = ThemeLibrary.WindowVars(title);
        var at = WindowRect(title);
        // Down to the bar's bottom: a modal's bar sits under the 40px band its title is in.
        var barBottom = bar is null ? at.Y + at.Height : WindowRect(bar).Y + WindowRect(bar).Height;
        var variant = new JsonObject
        {
            ["layout"] = new JsonObject
            {
                ["minHeight"] = ThemeNumbers.Json(barBottom),
                ["padding"] = 0,
            },
        };

        // `relative_horizontal_scale_center` centres the title across the frame whatever its `x`.
        if (ParamsOf(title).Contains("relative_horizontal_scale_center"))
            variant["captionTop"] = ThemeNumbers.Json(at.Y);
        else
            variant["captionAt"] = new JsonObject
            {
                ["left"] = ThemeNumbers.Json(at.X),
                ["top"] = ThemeNumbers.Json(at.Y),
            };

        if (menu is not null)
            variant["menuButton"] = new JsonObject
            {
                ["variant"] = menu.Attr("style") ?? row.Style,
                ["left"] = ThemeNumbers.Json(WindowRect(menu).X),
                ["top"] = ThemeNumbers.Json(WindowRect(menu).Y),
            };

        if (vars.GetValueOrDefault("text_style") is string { Length: > 0 } textStyle)
            variant["textStyle"] = textStyle;

        if (
            vars.GetValueOrDefault("text_color") is string { Length: > 0 } textColor
            && ThemeXml.HexColor(textColor) is { } color
        )
            variant["textColor"] = color;

        var cascade = new JsonObject();

        if (close is not null)
        {
            var box = WindowRect(close);

            // `relative_horizontal_scale_move` keeps its distance to the right edge; the rest, to the left.
            if (ParamsOf(close).Contains("relative_horizontal_scale_move"))
                variant["closeAt"] = new JsonObject
                {
                    ["right"] = ThemeNumbers.Json(layout.Width - (box.X + box.Width)),
                    ["top"] = ThemeNumbers.Json(box.Y),
                };

            cascade["closeButton"] = close.Attr("style") ?? row.Style;
        }

        return (variant, cascade);
    }

    /// <summary>The derived fields with the rule's <c>set</c> over them, <c>layout</c> key by key, then the rule's <c>unset</c> taken away.</summary>
    private static JsonObject ApplyRule(JsonObject derived, JsonObject? rule)
    {
        var set = rule?["set"] as JsonObject;
        var variant = ThemeJson.Copy(derived);

        ThemeJson.Assign(variant, set);

        if (derived["layout"] is JsonObject || set?["layout"] is JsonObject)
        {
            var layout = new JsonObject();

            ThemeJson.Assign(layout, derived["layout"] as JsonObject);
            ThemeJson.Assign(layout, set?["layout"] as JsonObject);
            variant["layout"] = layout;
        }

        foreach (var path in Strings(rule?["unset"]))
        {
            var parts = path.Split('.');
            var (field, key) = (parts[0], parts.Length > 1 ? parts[1] : "");

            if (key.Length > 0 && variant[field] is JsonObject inner)
                _ = inner.Remove(key);
            else if (key.Length == 0)
                _ = variant.Remove(field);
        }

        if (variant["layout"] is JsonObject { Count: 0 })
            _ = variant.Remove("layout");

        return variant;
    }

    /// <summary>A cascade rule's <c>unset</c> taken out of a cascade.</summary>
    private static void Unset(JsonObject cascade, JsonObject? rule)
    {
        foreach (var child in Strings(rule?["unset"]))
            _ = cascade.Remove(child);
    }

    private static IEnumerable<string> Strings(JsonNode? array) =>
        array is JsonArray list
            ? list.Select(x =>
                x is JsonValue value && value.TryGetValue<string>(out var text)
                    ? text
                    : x?.ToJsonString() ?? "null"
            )
            : [];

    /// <summary>JavaScript's truthiness of a JSON value.</summary>
    private static bool Truthy(JsonNode? node) =>
        node switch
        {
            null => false,
            JsonValue value when value.TryGetValue<bool>(out var flag) => flag,
            JsonValue value when value.TryGetValue<string>(out var text) => text.Length > 0,
            JsonValue value when value.TryGetValue<double>(out var number) => number != 0
                && !double.IsNaN(number),
            _ => true,
        };

    /// <summary>The rows of one element type, by style: a later row of a style in the place of the first.</summary>
    private static Dictionary<string, ThemeElementRow> RowsByStyle(
        List<ThemeElementRow> rows,
        string? type
    )
    {
        var byStyle = new Dictionary<string, ThemeElementRow>();

        foreach (var row in rows.Where(x => x.Type == type))
            byStyle[row.Style] = row;

        return byStyle;
    }

    /// <summary>
    /// An object's keys in the order JavaScript walks them: the integer-like keys (<c>"0"</c>,
    /// <c>"100"</c>) ascending, then the rest as written. The order variants are built in decides
    /// which of two sprites of one name is kept, so it is Studio's.
    /// </summary>
    public static IEnumerable<string> JsKeys(JsonObject obj)
    {
        var keys = obj.Select(x => x.Key).ToList();
        var indices = keys.Where(IsArrayIndex)
            .OrderBy(x => uint.Parse(x, System.Globalization.CultureInfo.InvariantCulture));

        return indices.Concat(keys.Where(x => !IsArrayIndex(x)));
    }

    private static bool IsArrayIndex(string key) =>
        ArrayIndex().IsMatch(key)
        && uint.TryParse(key, System.Globalization.CultureInfo.InvariantCulture, out var value)
        && value < uint.MaxValue;

    [GeneratedRegex(@"^(0|[1-9][0-9]*)\z")]
    private static partial Regex ArrayIndex();

    [GeneratedRegex(@"^icon_([0-9]+)\z")]
    private static partial Regex IconName();
}
