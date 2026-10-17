using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Turbo.Assets.Conversion;

/// <summary>
/// A Flash window template's <c>&lt;layout&gt;</c> XML as the data the client draws
/// (nitro-theme's <c>layoutToTemplate</c>, <c>TemplateData</c>): <c>WindowParser</c>'s reading of
/// each element, its <c>params</c> decoded by what each bit decides, its attributes unescaped and its
/// <c>&lt;var&gt;</c>s typed. The XML is read as nitro-theme reads it - a tag at a time, text and
/// comments passed over - so a layout Flash took is taken whatever else it holds.
/// </summary>
public static partial class LayoutTemplateMapper
{
    /// <summary><c>WindowParam</c>'s names, as <c>WindowParser</c> reads a <c>&lt;param name&gt;</c>.</summary>
    private static readonly Dictionary<string, uint> WINDOW_PARAMS = new(StringComparer.Ordinal)
    {
        ["null"] = 0,
        ["bound_to_parent_rect"] = 32,
        ["child_window"] = 33,
        ["embedded_controller"] = 51,
        ["expand_to_accommodate_children"] = 131072,
        ["input_event_processor"] = 1,
        ["internal_event_handling"] = 9,
        ["mouse_dragging_target"] = 32768,
        ["mouse_dragging_trigger"] = 257,
        ["mouse_scaling_target"] = 65536,
        ["mouse_scaling_trigger"] = 12288,
        ["horizontal_mouse_scaling_trigger"] = 4096,
        ["vertical_mouse_scaling_trigger"] = 8192,
        ["observe_parent_input_events"] = 5,
        ["parent_window"] = 1,
        ["resize_to_accommodate_children"] = 147456,
        ["relative_horizontal_scale_center"] = 192,
        ["relative_horizontal_scale_fixed"] = 0,
        ["relative_horizontal_scale_move"] = 64,
        ["relative_horizontal_scale_strech"] = 128,
        ["relative_scale_center"] = 3264,
        ["relative_scale_fixed"] = 0,
        ["relative_scale_move"] = 1088,
        ["relative_scale_strech"] = 2176,
        ["relative_vertical_scale_center"] = 3072,
        ["relative_vertical_scale_fixed"] = 0,
        ["relative_vertical_scale_move"] = 1024,
        ["relative_vertical_scale_strech"] = 2048,
        ["on_resize_align_left"] = 0,
        ["on_resize_align_right"] = 262144,
        ["on_resize_align_center"] = 786432,
        ["on_resize_align_top"] = 0,
        ["on_resize_align_bottom"] = 1048576,
        ["on_resize_align_middle"] = 3145728,
        ["on_accommodate_align_left"] = 0,
        ["on_accommodate_align_right"] = 262144,
        ["on_accommodate_align_center"] = 786432,
        ["on_accommodate_align_top"] = 0,
        ["on_accommodate_align_bottom"] = 1048576,
        ["on_accommodate_align_middle"] = 3145728,
        ["route_input_events_to_parent"] = 3,
        ["use_parent_graphic_context"] = 16,
        ["draggable_with_mouse"] = 33025,
        ["scalable_with_mouse"] = 77824,
        ["reflect_horizontal_resize_to_parent"] = 4194304,
        ["reflect_vertical_resize_to_parent"] = 8388608,
        ["reflect_resize_to_parent"] = 12582912,
        ["force_clipping"] = 1073741824,
        ["inherit_caption"] = 2147483648,
    };

    /// <summary>The input bits, by the bit each one sets.</summary>
    private static readonly (int Bit, string Name)[] EVENT_BITS =
    [
        (0, "input"),
        (1, "routeToParent"),
        (2, "observeParent"),
        (3, "internal"),
        (8, "dragTrigger"),
        (12, "scaleTriggerH"),
        (13, "scaleTriggerV"),
        (15, "dragTarget"),
        (16, "scaleTarget"),
    ];

    private static readonly string[] SCALES = ["fixed", "move", "stretch", "center"];

    // Bits 18-19 and 20-21: only 0, 1 and 3 are named (bit 19 or 21 alone is not).
    private static readonly string?[] ALIGN_H = ["left", "right", null, "center"];
    private static readonly string?[] ALIGN_V = ["top", "bottom", null, "middle"];

    /// <summary>What sits beside a window's elements in its XML rather than being one.</summary>
    private static readonly HashSet<string> STRUCTURE = new(StringComparer.Ordinal)
    {
        "variables",
        "params",
        "filters",
        "children",
    };

    private static readonly Dictionary<string, string> ENTITIES = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["quot"] = "\"",
        ["apos"] = "'",
        ["lt"] = "<",
        ["gt"] = ">",
        ["amp"] = "&",
    };

    /// <summary>An element of a layout as the reader takes it: its tag, attributes and child elements.</summary>
    public sealed record Node(string Tag, Dictionary<string, string> Attrs, List<Node> Children);

    /// <summary>
    /// A <c>&lt;layout&gt;</c> document as a template - <c>name</c>, <c>width</c>, <c>height</c> and
    /// its window's <c>elements</c> - or null when the text holds no layout window. Throws
    /// <see cref="FormatException"/> for a <c>&lt;param name&gt;</c> Flash does not know, as
    /// <c>WindowParser</c> refuses the layout.
    /// </summary>
    /// <param name="resolveAsset">A bitmap's name (<c>asset_uri</c>, <c>bitmap_asset_name</c>) as the client will find it; null keeps it as it stands.</param>
    /// <param name="frameMargins">A frame's or bubble's content area (left, top, right, bottom); null for none.</param>
    public static JsonObject? Map(
        string xml,
        Func<string, string?>? resolveAsset = null,
        Func<JsonObject, int[]?>? frameMargins = null
    )
    {
        var layout = Parse(xml);
        var window = layout?.Tag == "layout" ? Child(layout, "window") : null;

        if (layout is null || window is null)
            return null;

        return new JsonObject
        {
            ["name"] = layout.Attrs.GetValueOrDefault("name") ?? string.Empty,
            ["width"] = Number(layout.Attrs.GetValueOrDefault("width") ?? "0"),
            ["height"] = Number(layout.Attrs.GetValueOrDefault("height") ?? "0"),
            ["elements"] = new JsonArray([
                .. window
                    .Children.Where(x => !STRUCTURE.Contains(x.Tag))
                    .Select(x => (JsonNode)Element(x, resolveAsset, frameMargins)),
            ]),
        };
    }

    /// <summary>
    /// The first element of an XML text, as nitro-theme's <c>parseLayoutXml</c> reads it: element
    /// tags and their attributes (entities decoded), text, comments, CDATA and declarations passed
    /// over, a closing tag ending the innermost open element whatever it names.
    /// </summary>
    public static Node? Parse(string text)
    {
        var root = new Node("#root", [], []);
        var stack = new Stack<Node>();

        stack.Push(root);

        foreach (Match match in TokenPattern().Matches(text))
        {
            if (match.Groups[1].Success)
            {
                if (stack.Count > 1)
                    stack.Pop();

                continue;
            }

            if (!match.Groups[2].Success)
                continue;

            var node = new Node(match.Groups[2].Value, new(StringComparer.Ordinal), []);

            foreach (Match attribute in AttributePattern().Matches(match.Groups[3].Value))
                node.Attrs[attribute.Groups[1].Value] = DecodeEntities(
                    attribute.Groups[2].Success
                        ? attribute.Groups[2].Value
                        : attribute.Groups[3].Value
                );

            stack.Peek().Children.Add(node);

            if (match.Groups[4].Value.Length == 0)
                stack.Push(node);
        }

        return root.Children.FirstOrDefault();
    }

    /// <summary>The tag of a text's first element, its declaration and comments passed over.</summary>
    public static string? RootTag(string text)
    {
        var match = RootTagPattern()
            .Match(CommentPattern().Replace(DeclarationPattern().Replace(text, ""), ""));

        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Where a window layout's <c>content_area</c> leaves its edges (<c>FrameController.margins</c>:
    /// left, top, right, bottom); null when it has none.
    /// </summary>
    public static int[]? ContentMargins(JsonObject? skin)
    {
        if (skin is null)
            return null;

        var area = Find(skin["elements"] as JsonArray);

        if (area is null)
            return null;

        int Int(JsonNode? node, string key) =>
            node?[key] is JsonValue value && value.TryGetValue<double>(out var number)
                ? (int)number
                : 0;

        var x = Int(area, "x");
        var y = Int(area, "y");

        return
        [
            x,
            y,
            Int(skin, "width") - x - Int(area, "width"),
            Int(skin, "height") - y - Int(area, "height"),
        ];

        static JsonObject? Find(JsonArray? elements)
        {
            foreach (var element in elements?.OfType<JsonObject>() ?? [])
            {
                var found =
                    element["name"]?.GetValue<string>() == "content_area"
                        ? element
                        : Find(element["children"] as JsonArray);

                if (found is not null)
                    return found;
            }

            return null;
        }
    }

    /// <summary>A frame's or bubble's content area: its window layout's, each edge under its own <c>margin_&lt;edge&gt;</c> variable.</summary>
    public static int[] FrameMargins(JsonObject frame, int[]? @base)
    {
        @base ??= [0, 0, 0, 0];

        var vars = frame["vars"] as JsonObject;

        int Own(string edge, int fallback)
        {
            if (vars?[$"margin_{edge}"] is not JsonValue value)
                return fallback;

            double? number = value.GetValueKind() switch
            {
                System.Text.Json.JsonValueKind.Number => value.GetValue<double>(),
                System.Text.Json.JsonValueKind.True => 1,
                System.Text.Json.JsonValueKind.False => 0,
                System.Text.Json.JsonValueKind.String => JsValues.ToNumber(
                    value.GetValue<string>()
                ),
                _ => null,
            };

            return number is { } n && double.IsFinite(n) ? (int)n : fallback;
        }

        return
        [
            Own("left", @base[0]),
            Own("top", @base[1]),
            Own("right", @base[2]),
            Own("bottom", @base[3]),
        ];
    }

    private static JsonObject Element(
        Node node,
        Func<string, string?>? resolveAsset,
        Func<JsonObject, int[]?>? frameMargins
    )
    {
        var attrs = node.Attrs;
        var variables =
            Child(node, "variables")?.Children.Where(x => x.Tag == "var").ToList() ?? [];
        var vars = new JsonObject();

        foreach (var entry in variables)
            vars[entry.Attrs.GetValueOrDefault("key") ?? "undefined"] = VarValue(
                entry,
                resolveAsset
            );

        var element = new JsonObject
        {
            ["tag"] = node.Tag,
            ["x"] = NumberOr(attrs, "x"),
            ["y"] = NumberOr(attrs, "y"),
            ["width"] = NumberOr(attrs, "width"),
            ["height"] = NumberOr(attrs, "height"),
            ["vars"] = vars,
            ["children"] = new JsonArray([
                .. (Child(node, "children")?.Children ?? []).Select(x =>
                    (JsonNode)Element(x, resolveAsset, frameMargins)
                ),
            ]),
        };

        if (attrs.GetValueOrDefault("name") is { Length: > 0 } name)
            element["name"] = Unescape(name);

        var limits = new[] { "width_min", "width_max", "height_min", "height_max" };

        if (limits.Any(attrs.ContainsKey))
            element["limits"] = new JsonArray([
                .. limits.Select(x => attrs.TryGetValue(x, out var limit) ? Number(limit) : null),
            ]);

        if (attrs.GetValueOrDefault("style") is { Length: > 0 } style && style != "0")
            element["style"] = style;

        if (attrs.GetValueOrDefault("dynamic_style") is { Length: > 0 } dynamicStyle)
            element["dynamicStyle"] = dynamicStyle;

        if (DecodeParams(ReadParams(node)) is { } windowParams)
            element["params"] = windowParams;

        if (attrs.TryGetValue("caption", out var caption))
            element["caption"] = Unescape(caption);

        if (attrs.GetValueOrDefault("visible") == "false")
            element["hidden"] = true;

        if (attrs.TryGetValue("color", out var color))
            element["color"] = FlashUint(color);

        if (attrs.GetValueOrDefault("background") == "true")
            element["background"] = true;

        if (attrs.TryGetValue("blend", out var blend) && JsValues.ToNumber(blend) != 1)
            element["blend"] = Number(blend);

        if (attrs.GetValueOrDefault("clipping") == "false")
            element["clipping"] = false;

        if (attrs.TryGetValue("treshold", out var threshold) && JsValues.ToNumber(threshold) != 10)
            element["mouseThreshold"] = Number(threshold);

        if (attrs.GetValueOrDefault("tags") is { Length: > 0 } tags)
            element["tags"] = new JsonArray([
                .. Unescape(tags)
                    .Split(',')
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0)
                    .Select(x => (JsonNode)x),
            ]);

        // WindowParser's DropShadowFilter, with its defaults; GraphicContext draws the first outer one.
        var shadow = (Child(node, "filters")?.Children ?? []).FirstOrDefault(x =>
            x.Tag == "DropShadowFilter" && x.Attrs.GetValueOrDefault("inner") != "true"
        );

        if (shadow is not null)
        {
            double? Value(string key, double fallback) =>
                shadow.Attrs.TryGetValue(key, out var value) ? JsValues.ToNumber(value) : fallback;

            var blurX = Value("blurX", 0);
            var blurY = Value("blurY", 0);

            element["dropShadow"] = new JsonObject
            {
                ["distance"] = JsValues.Number(Value("distance", 0)),
                ["angle"] = JsValues.Number(Value("angle", 45)),
                ["color"] = JsValues.Number(Value("color", 0)),
                ["alpha"] = JsValues.Number(Value("alpha", 1)),
                ["blur"] = JsValues.Number(
                    blurX is { } bx && blurY is { } by ? Math.Max(0, Math.Max(bx, by)) : null
                ),
            };
        }

        // FrameController and BubbleController, which extends it: children go into a content area.
        if (node.Tag is "frame" or "bubble" && frameMargins?.Invoke(element) is { } margins)
            element["margins"] = new JsonArray([.. margins.Select(x => (JsonNode)x)]);

        return element;
    }

    private static JsonNode? VarValue(Node node, Func<string, string?>? resolveAsset)
    {
        if (Child(node, "value")?.Children.FirstOrDefault() is { } nested)
        {
            var entries = nested.Children.Where(x => x.Tag == "var").ToList();

            if (nested.Tag == "Array")
                return new JsonArray([.. entries.Select(x => VarValue(x, resolveAsset))]);

            var map = new JsonObject();

            foreach (var entry in entries)
                map[entry.Attrs.GetValueOrDefault("key") ?? "undefined"] = VarValue(
                    entry,
                    resolveAsset
                );

            return map;
        }

        var key = node.Attrs.GetValueOrDefault("key");
        var value = node.Attrs.GetValueOrDefault("value") ?? string.Empty;

        // A shared variable the calling code fills in when it builds the window.
        if (value.StartsWith('$') && !value.StartsWith("${", StringComparison.Ordinal))
            return value;

        switch (node.Attrs.GetValueOrDefault("type"))
        {
            case "Boolean":
                return value == "true";
            case "hex":
            case "uint":
                return FlashUint(value);
            case "int":
            case "Integer":
            case "Number":
                return Number(value);
            default:
                if (key is "asset_uri" or "bitmap_asset_name" && value.Length > 0)
                    return resolveAsset?.Invoke(value) ?? value;

                return value;
        }
    }

    private static uint ReadParams(Node node)
    {
        var value = FlashUint(node.Attrs.GetValueOrDefault("params") ?? "0");

        foreach (var param in Child(node, "params")?.Children ?? [])
        {
            var name = param.Attrs.GetValueOrDefault("name") ?? "undefined";

            if (!WINDOW_PARAMS.TryGetValue(name, out var flag))
                throw new FormatException($"Unknown window parameter \"{name}\"!");

            value |= flag;
        }

        return value;
    }

    /// <summary>A <c>params</c> value split by what each part decides; null when it sets nothing.</summary>
    private static JsonObject? DecodeParams(uint value)
    {
        bool Bit(int index) => ((value >> index) & 1) == 1;

        var output = new JsonObject();
        var events = EVENT_BITS.Where(x => Bit(x.Bit)).Select(x => x.Name).ToList();
        var scaleH = SCALES[(value >> 6) & 3];
        var scaleV = SCALES[(value >> 10) & 3];
        var alignH = ALIGN_H[(value >> 18) & 3];
        var alignV = ALIGN_V[(value >> 20) & 3];
        var unnamed = new[] { 9, 24, 25, 26, 27, 28, 29 }.Where(Bit).ToList();

        if (events.Count > 0)
            output["events"] = new JsonArray([.. events.Select(x => (JsonNode)x)]);

        if (Bit(4))
            output["parentGraphics"] = true;

        if (Bit(5))
            output["boundToParent"] = true;

        if (scaleH != "fixed" || scaleV != "fixed")
            output["scale"] = new JsonArray(scaleH, scaleV);

        if (Bit(17))
            output["accommodate"] = Bit(14) ? "resize" : "expand";
        else if (Bit(14))
            unnamed.Add(14);

        if (alignH is null)
            unnamed.Add(19);

        if (alignV is null)
            unnamed.Add(21);

        if ((alignH is not null && alignH != "left") || (alignV is not null && alignV != "top"))
            output["align"] = new JsonArray(alignH ?? "left", alignV ?? "top");

        if (Bit(22) || Bit(23))
            output["reflectToParent"] = new JsonArray(Bit(22), Bit(23));

        if (Bit(30))
            output["forceClipping"] = true;

        if (Bit(31))
            output["inheritCaption"] = true;

        if (unnamed.Count > 0)
            output["unnamedBits"] = new JsonArray([.. unnamed.Order().Select(x => (JsonNode)x)]);

        return output.Count > 0 ? output : null;
    }

    /// <summary>
    /// Flash's <c>uint("...")</c> as nitro-theme reads it: the low 32 bits of a whole number
    /// (decimal, <c>0x</c>, <c>0o</c>, <c>0b</c>), else JavaScript's <c>Number(value) &gt;&gt;&gt; 0</c>.
    /// </summary>
    internal static uint FlashUint(string value)
    {
        var text = value.Trim();
        BigInteger? whole = null;

        if (text.Length == 0)
            whole = 0;
        else if (DecimalPattern().IsMatch(text))
            whole = BigInteger.Parse(
                text,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture
            );
        else if (
            text.Length > 2
            && text[0] == '0'
            && char.ToLowerInvariant(text[1]) is 'x' or 'o' or 'b'
        )
        {
            var radix = char.ToLowerInvariant(text[1]) switch
            {
                'x' => 16,
                'o' => 8,
                _ => 2,
            };
            BigInteger parsed = 0;

            foreach (var c in text[2..])
            {
                var digit = c switch
                {
                    >= '0' and <= '9' => c - '0',
                    >= 'a' and <= 'f' => c - 'a' + 10,
                    >= 'A' and <= 'F' => c - 'A' + 10,
                    _ => 99,
                };

                if (digit >= radix)
                {
                    parsed = -1;

                    break;
                }

                parsed = (parsed * radix) + digit;
            }

            whole = parsed < 0 ? null : parsed;
        }

        if (whole is { } big)
            return (uint)(big & uint.MaxValue);

        // ToUint32: NaN and the infinities are 0, the rest truncated and wrapped.
        if (JsValues.ToNumber(value) is not { } number || !double.IsFinite(number))
            return 0;

        var truncated = Math.Truncate(number) % 4294967296d;

        return (uint)(truncated < 0 ? truncated + 4294967296d : truncated);
    }

    /// <summary>Flash's <c>unescape</c>: <c>%XX</c> and <c>%uXXXX</c> sequences.</summary>
    private static string Unescape(string value) =>
        EscapePattern()
            .Replace(
                value,
                match =>
                    (
                        (char)
                            int.Parse(
                                match.Groups[1].Success
                                    ? match.Groups[1].Value
                                    : match.Groups[2].Value,
                                NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture
                            )
                    ).ToString()
            );

    private static string DecodeEntities(string value) =>
        EntityPattern()
            .Replace(
                value,
                match =>
                {
                    var entity = match.Groups[1].Value;

                    if (entity[0] != '#')
                        return ENTITIES.GetValueOrDefault(entity) ?? match.Value;

                    var code =
                        entity.Length > 1 && entity[1] is 'x' or 'X'
                            ? int.Parse(
                                entity[2..],
                                NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture
                            )
                            : int.Parse(entity[1..], CultureInfo.InvariantCulture);

                    return code is >= 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF)
                        ? char.ConvertFromUtf32(code)
                        : match.Value;
                }
            );

    private static Node? Child(Node node, string tag) =>
        node.Children.FirstOrDefault(x => x.Tag == tag);

    /// <summary><c>Number(text)</c> as JSON: null for NaN.</summary>
    private static JsonNode? Number(string text) => JsValues.Number(JsValues.ToNumber(text));

    /// <summary>An attribute's number, 0 when the element has none.</summary>
    private static JsonNode? NumberOr(Dictionary<string, string> attrs, string key) =>
        attrs.TryGetValue(key, out var value) ? Number(value) : JsValues.Number(0);

    [GeneratedRegex(
        @"<!--[\s\S]*?-->|<!\[CDATA\[[\s\S]*?\]\]>|<[?!][\s\S]*?>|</([\w:.-]+)\s*>|<([\w:.-]+)((?:\s+[\w:.-]+\s*=\s*(?:""[^""]*""|'[^']*'))*)\s*(/?)>"
    )]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"([\w:.-]+)\s*=\s*(?:""([^""]*)""|'([^']*)')")]
    private static partial Regex AttributePattern();

    [GeneratedRegex("&(quot|apos|lt|gt|amp|#\\d+|#x[0-9a-f]+);", RegexOptions.IgnoreCase)]
    private static partial Regex EntityPattern();

    [GeneratedRegex("%u([0-9a-fA-F]{4})|%([0-9a-fA-F]{2})")]
    private static partial Regex EscapePattern();

    [GeneratedRegex(@"<\s*([A-Za-z_][\w-]*)[\s>/]")]
    private static partial Regex RootTagPattern();

    [GeneratedRegex(@"<\?xml[^>]*\?>")]
    private static partial Regex DeclarationPattern();

    [GeneratedRegex("<!--[\\s\\S]*?-->")]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"^[+-]?\d+$")]
    private static partial Regex DecimalPattern();
}
