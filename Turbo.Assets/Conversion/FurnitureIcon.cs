using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Turbo.Assets.Nitro;

namespace Turbo.Assets.Conversion;

/// <summary>
/// A furniture's icon drawn from its own bundle, as the renderer draws a furniture at its icon size
/// (<c>FurnitureVisualization</c> at size 1) - ported from nitro-studio's <c>shared/furni-icon.ts</c>
/// and <c>server/furni-icon.ts</c> at nitro-next <c>1e0843c3</c>, so the hotel's icons are drawn as
/// the Studio draws them, pixel for pixel:
/// <list type="bullet">
/// <item>its layers are the icon visualization's <c>layerCount</c>; layer <c>n</c> draws the asset
/// <c>&lt;type&gt;_icon_&lt;letter n&gt;</c>, or - a bundle with none of those - the first asset named
/// like an icon for each letter;</item>
/// <item>each layer is placed at its asset's offset (<c>-x, -y</c>; mirrored, <c>x - width</c>) plus
/// its layer's <c>x</c>, <c>y</c>, with its layer's <c>alpha</c> and <c>ink</c>, tinted by the colour's
/// tint for it; a trimmed sheet frame is drawn in its whole box;</item>
/// <item>layers are drawn back to front by <c>z + id</c>, and the icon is the bounds of every layer
/// drawn. A group furniture's colour layers are drawn in a group's default colours, its badge not.</item>
/// </list>
/// </summary>
public static partial class FurnitureIcon
{
    public const int ICON_SIZE = 1;

    /// <summary>The largest icon drawn: Habbo's are some 50 pixels.</summary>
    public const int MAX_ICON = 1024;

    private const string LETTERS = "abcdefghijklmnopqrstuvwxyz";

    private static readonly HashSet<string> GUILD_VISUALIZATIONS = new(StringComparer.Ordinal)
    {
        "furniture_guild_customized",
        "furniture_guild_isometric_badge",
    };

    private static readonly Dictionary<string, int> GUILD_COLORS = new(StringComparer.Ordinal)
    {
        ["COLOR1"] = 0xEEEEEE,
        ["COLOR2"] = 0x4B4B4B,
    };

    private const string GUILD_BADGE_TAG = "BADGE";

    /// <summary>
    /// The icon of <paramref name="name"/> (the bundle's asset name) in colour
    /// <paramref name="colorId"/> (0 the plain one), as straight RGBA; null when the bundle has nothing
    /// to draw it from - no icon visualization, or no icon frame on its sheet.
    /// </summary>
    public static RgbaImage? Draw(NitroBundle bundle, string name, int colorId = 0)
    {
        var data = AssetDataOf(bundle, name);
        var visualization = IconVisualizationOf(data);
        var sheetFile = SheetImageOf(bundle, name);

        if (data is null || visualization is null || sheetFile is null)
            return null;

        var frames = FramesOf(bundle, name);
        var assets = DefinedAssets(data["assets"] as JsonArray, frames);
        var settings = LayerSettings(visualization);
        var iconAsset = IconAssets(Text(data["type"]) ?? name, assets, settings.Count);
        RgbaImage? sheet = null;
        var parts = new List<Part>();
        var tints = Tints(visualization, colorId);
        var guild = GUILD_VISUALIZATIONS.Contains(Text(data["visualizationType"]) ?? "");

        for (var layerId = 0; layerId < settings.Count; layerId++)
        {
            var layer = settings[layerId];

            if (guild && layer.Tag == GUILD_BADGE_TAG)
                continue;

            if (
                iconAsset(layerId) is not { } asset
                || !frames.TryGetValue(asset.Source, out var frame)
            )
                continue;

            sheet ??= RgbaImage.Decode(bundle.Files[sheetFile]);

            // A frame inside its pixels, near the origin: a bundle's numbers are the uploader's.
            if (
                frame.X < 0
                || frame.Y < 0
                || frame.W <= 0
                || frame.H <= 0
                || frame.X + frame.W > sheet.Width
                || frame.Y + frame.H > sheet.Height
                || frame.TrimX < 0
                || frame.TrimY < 0
                || frame.TrimX + frame.W > frame.BoxW
                || frame.TrimY + frame.H > frame.BoxH
                || frame.BoxW > MAX_ICON
                || frame.BoxH > MAX_ICON
            )
                continue;

            if (new[] { asset.X, asset.Y, layer.X, layer.Y }.Any(x => Math.Abs(x) > MAX_ICON))
                continue;

            // GraphicAsset.offsetX: -x, or x mirrored (the sprite then spans to its left).
            var anchorX = (asset.FlipH ? asset.X : -asset.X) + layer.X;
            var anchorY = (asset.FlipV ? asset.Y : -asset.Y) + layer.Y;

            parts.Add(
                new Part(
                    layer.Z + layerId,
                    asset,
                    frame,
                    asset.FlipH ? anchorX - frame.BoxW : anchorX,
                    asset.FlipV ? anchorY - frame.BoxH : anchorY,
                    layer,
                    guild && GUILD_COLORS.TryGetValue(layer.Tag, out var guildColor)
                        ? guildColor
                        : tints.GetValueOrDefault(layerId, 0xFFFFFF)
                )
            );
        }

        if (parts.Count == 0 || sheet is null)
            return null;

        // Stable: equal depths keep their layer order.
        parts = [.. parts.OrderBy(x => x.Order)];

        var minX = parts.Min(x => x.Left);
        var minY = parts.Min(x => x.Top);
        var width = parts.Max(x => x.Left + x.Frame.BoxW) - minX;
        var height = parts.Max(x => x.Top + x.Frame.BoxH) - minY;

        if (width > MAX_ICON || height > MAX_ICON || width <= 0 || height <= 0)
            return null;

        return Compose(parts, sheet, minX, minY, width, height);
    }

    /// <summary>Whether a bundle has an icon to draw: an icon visualization and a layer whose asset is on its sheet.</summary>
    public static bool Draws(NitroBundle bundle, string name)
    {
        var data = AssetDataOf(bundle, name);
        var visualization = IconVisualizationOf(data);

        if (data is null || visualization is null)
            return false;

        var frames = FramesOf(bundle, name);
        var settings = LayerSettings(visualization);
        var iconAsset = IconAssets(
            Text(data["type"]) ?? name,
            DefinedAssets(data["assets"] as JsonArray, frames),
            settings.Count
        );

        return Enumerable.Range(0, settings.Count).Any(x => iconAsset(x) is not null);
    }

    /// <summary>The colour ids the icon visualization tints, besides the plain icon.</summary>
    public static IReadOnlyList<int> ColorsOf(NitroBundle bundle, string name) =>
        IconVisualizationOf(AssetDataOf(bundle, name))?["colors"] is JsonArray colors
            ? [.. colors.OfType<JsonObject>().Select(x => Int(x["id"])).OfType<int>()]
            : [];

    private static RgbaImage Compose(
        List<Part> parts,
        RgbaImage sheet,
        int minX,
        int minY,
        int width,
        int height
    )
    {
        // Premultiplied, as the GPU blends: worked in doubles and kept in floats, as the Studio's
        // Float32Array keeps them, so the icons come out alike to the pixel; written out straight.
        var canvas = new float[width * height * 4];
        var data = sheet.Pixels;

        foreach (var part in parts)
        {
            var alpha = Clamp(part.Layer.Alpha / 255);

            if (alpha == 0)
                continue;

            double[] tint =
            [
                ((part.Tint >> 16) & 0xFF) / 255.0,
                ((part.Tint >> 8) & 0xFF) / 255.0,
                (part.Tint & 0xFF) / 255.0,
            ];
            var ink = part.Layer.Ink;
            var frame = part.Frame;

            for (var row = 0; row < frame.H; row++)
            {
                var boxY = part.Asset.FlipV
                    ? frame.BoxH - 1 - (frame.TrimY + row)
                    : frame.TrimY + row;

                for (var column = 0; column < frame.W; column++)
                {
                    var source = (((frame.Y + row) * sheet.Width) + frame.X + column) * 4;
                    var sa = (data[source + 3] / 255.0) * alpha;

                    if (sa == 0)
                        continue;

                    var boxX = part.Asset.FlipH
                        ? frame.BoxW - 1 - (frame.TrimX + column)
                        : frame.TrimX + column;
                    var target =
                        (((part.Top - minY + boxY) * width) + (part.Left - minX + boxX)) * 4;
                    double da = canvas[target + 3];

                    for (var channel = 0; channel < 3; channel++)
                    {
                        var s = (data[source + channel] / 255.0) * tint[channel] * sa;

                        canvas[target + channel] = (float)Clamp(
                            Blend(ink, s, sa, canvas[target + channel], da)
                        );
                    }

                    canvas[target + 3] = (float)(
                        ink switch
                        {
                            "add" => Math.Min(1, sa + da),
                            "subtract" => da,
                            _ => sa + (da * (1 - sa)),
                        }
                    );
                }
            }
        }

        var pixels = new byte[width * height * 4];

        for (var index = 0; index < canvas.Length; index += 4)
        {
            double a = canvas[index + 3];

            if (a <= 0)
                continue;

            for (var channel = 0; channel < 3; channel++)
                pixels[index + channel] = Round(Clamp(canvas[index + channel] / a) * 255);

            pixels[index + 3] = Round(a * 255);
        }

        return new RgbaImage(width, height, pixels);
    }

    /// <summary>One channel blended in premultiplied terms: <c>s</c>/<c>d</c> the colour times its alpha, <c>sa</c>/<c>da</c> the alphas.</summary>
    private static double Blend(string ink, double s, double sa, double d, double da) =>
        ink switch
        {
            "add" => Math.Min(1, s + d),
            "subtract" => Math.Max(0, d - s),
            "multiply" => (s * d) + (s * (1 - da)) + (d * (1 - sa)),
            "screen" => s + d - (s * d),
            "darken" => Math.Min(s * da, d * sa) + (s * (1 - da)) + (d * (1 - sa)),
            "lighten" => Math.Max(s * da, d * sa) + (s * (1 - da)) + (d * (1 - sa)),
            "difference" => s + d - (2 * Math.Min(s * da, d * sa)),
            _ => s + (d * (1 - sa)),
        };

    private static double Clamp(double value) =>
        value < 0 ? 0
        : value > 1 ? 1
        : value;

    // JavaScript's Math.round, for the values here (never negative).
    private static byte Round(double value) => (byte)Math.Floor(value + 0.5);

    /// <summary>The bundle's asset data: <c>&lt;name&gt;.json</c>, else its one that is not a sheet's.</summary>
    private static JsonObject? AssetDataOf(NitroBundle bundle, string name)
    {
        var file = bundle.Files.ContainsKey($"{name}.json")
            ? $"{name}.json"
            : bundle.Files.Keys.FirstOrDefault(x =>
                x.EndsWith(".json", StringComparison.Ordinal)
                && !x.EndsWith("_spritesheet.json", StringComparison.Ordinal)
            );

        if (file is null)
            return null;

        try
        {
            return JsonNode.Parse(bundle.Files[file]) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The size-1 visualization, when the bundle has one.</summary>
    private static JsonObject? IconVisualizationOf(JsonObject? data) =>
        (data?["visualizations"] as JsonArray)
            ?.OfType<JsonObject>()
            .FirstOrDefault(x => Int(x["size"]) == ICON_SIZE);

    private static JsonObject? SheetOf(NitroBundle bundle, string name)
    {
        var file = bundle.Files.ContainsKey($"{name}_spritesheet.json")
            ? $"{name}_spritesheet.json"
            : bundle.Files.Keys.FirstOrDefault(x =>
                x.EndsWith("_spritesheet.json", StringComparison.Ordinal)
            );

        if (file is null)
            return null;

        try
        {
            return JsonNode.Parse(bundle.Files[file]) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? SheetImageOf(NitroBundle bundle, string name)
    {
        var meta = Text(SheetOf(bundle, name)?["meta"]?["image"]);

        if (meta is not null && bundle.Files.ContainsKey(meta))
            return meta;

        return bundle.Files.ContainsKey($"{name}.png")
            ? $"{name}.png"
            : bundle.Files.Keys.FirstOrDefault(x => x.EndsWith(".png", StringComparison.Ordinal));
    }

    /// <summary>The sheet's frames by name, and by name without its extension, trimmed ones with their box.</summary>
    private static Dictionary<string, Frame> FramesOf(NitroBundle bundle, string name)
    {
        var frames = new Dictionary<string, Frame>(StringComparer.Ordinal);

        if (SheetOf(bundle, name)?["frames"] is not JsonObject raw)
            return frames;

        foreach (var (key, value) in raw)
        {
            if (value is not JsonObject entry || entry["frame"] is not JsonObject box)
                continue;

            var trimmed =
                entry["trimmed"]?.GetValueKind() == JsonValueKind.True
                && entry["sourceSize"] is JsonObject
                && entry["spriteSourceSize"] is JsonObject;
            var frame = new Frame(
                Int(box["x"]) ?? 0,
                Int(box["y"]) ?? 0,
                Int(box["w"]) ?? 0,
                Int(box["h"]) ?? 0,
                trimmed ? Int(entry["spriteSourceSize"]!["x"]) ?? 0 : 0,
                trimmed ? Int(entry["spriteSourceSize"]!["y"]) ?? 0 : 0,
                trimmed ? Int(entry["sourceSize"]!["w"]) ?? 0 : Int(box["w"]) ?? 0,
                trimmed ? Int(entry["sourceSize"]!["h"]) ?? 0 : Int(box["h"]) ?? 0
            );

            frames[key] = frame;

            var dot = key.LastIndexOf('.');

            frames[dot < 0 ? key : key[..dot]] = frame;
        }

        return frames;
    }

    /// <summary>The assets by name as the renderer defines them: the first wins, unless it is an alias of another.</summary>
    private static Dictionary<string, Asset> DefinedAssets(
        JsonArray? assets,
        Dictionary<string, Frame> frames
    )
    {
        var defined = new Dictionary<string, Asset>(StringComparer.Ordinal);

        foreach (var item in assets?.OfType<JsonObject>() ?? [])
        {
            if (Text(item["name"]) is not { Length: > 0 } name)
                continue;

            var source = Text(item["source"]) ?? name;

            if (!frames.ContainsKey(source))
                continue;

            if (defined.TryGetValue(name, out var existing) && existing.Source == existing.Name)
                continue;

            defined[name] = new Asset(
                name,
                source,
                Int(item["x"]) ?? 0,
                Int(item["y"]) ?? 0,
                item["flipH"]?.GetValueKind() == JsonValueKind.True,
                item["flipV"]?.GetValueKind() == JsonValueKind.True
            );
        }

        return defined;
    }

    /// <summary>
    /// Each layer's icon asset: <c>&lt;type&gt;_icon_&lt;letter&gt;</c>; a bundle with none of those has
    /// it from the assets named like icons all the same, the first for each letter.
    /// </summary>
    private static Func<int, Asset?> IconAssets(
        string type,
        Dictionary<string, Asset> assets,
        int layerCount
    )
    {
        Asset? Exact(int layerId) => assets.GetValueOrDefault($"{type}_icon_{LETTERS[layerId]}");

        if (Enumerable.Range(0, layerCount).Any(x => Exact(x) is not null))
            return Exact;

        var loose = new Dictionary<char, Asset>();

        foreach (var (name, asset) in assets)
        {
            var match = LooseIconPattern().Match(name);

            if (match.Success)
                loose.TryAdd(match.Groups[1].Value[0], asset);
        }

        return layerId => loose.GetValueOrDefault(LETTERS[layerId]);
    }

    /// <summary>Each layer's settings as the icon is drawn: the size's layers, then those of the direction it is drawn in.</summary>
    private static List<Layer> LayerSettings(JsonObject visualization)
    {
        var count = Math.Clamp(Int(visualization["layerCount"]) ?? 0, 0, LETTERS.Length);
        var layers = Enumerable.Range(0, count).Select(_ => new Layer()).ToList();

        Apply(layers, visualization["layers"] as JsonArray);
        Apply(layers, IconDirection(visualization)?["layers"] as JsonArray);

        return layers;
    }

    /// <summary><c>SizeData.setDirectionLayers</c>: settings over the ones there, stopping at the first layer out of range.</summary>
    private static void Apply(List<Layer> into, JsonArray? layers)
    {
        foreach (var layer in layers?.OfType<JsonObject>() ?? [])
        {
            var id = Int(layer["id"]) ?? -1;

            if (id < 0 || id >= into.Count)
                return;

            var target = into[id];

            if (layer["tag"] is JsonValue tag)
                target.Tag = Text(tag) ?? tag.ToJsonString();

            if (Text(layer["ink"]) is { } ink)
                target.Ink = ink.ToLowerInvariant();

            if (Number(layer["alpha"]) is { } alpha)
                target.Alpha = alpha;

            if (Int(layer["x"]) is { } x)
                target.X = x;

            if (Int(layer["y"]) is { } y)
                target.Y = y;

            if (Int(layer["z"]) is { } z)
                target.Z = z;
        }
    }

    /// <summary><c>SizeData.getValidDirection(0)</c>: direction 0, else the one whose angle is nearest it.</summary>
    private static JsonObject? IconDirection(JsonObject visualization)
    {
        var directions =
            (visualization["directions"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];

        if (directions.FirstOrDefault(x => Int(x["id"]) == 0) is { } straight)
            return straight;

        var angle = Int(visualization["angle"]) ?? 360;
        JsonObject? best = null;
        var bestAngle = -1;

        foreach (var direction in directions)
        {
            var difference = (((Int(direction["id"]) ?? 0) * angle % 360) + 360) % 360;

            if (difference > 180)
                difference = 360 - difference;

            if (bestAngle < 0 || difference < bestAngle)
            {
                bestAngle = difference;
                best = direction;
            }
        }

        return best;
    }

    /// <summary>The tint of each layer for a colour of the icon size: none where it has none.</summary>
    private static Dictionary<int, int> Tints(JsonObject visualization, int colorId)
    {
        var tints = new Dictionary<int, int>();
        var color = (visualization["colors"] as JsonArray)
            ?.OfType<JsonObject>()
            .FirstOrDefault(x => Int(x["id"]) == colorId);

        foreach (var layer in (color?["layers"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            if (Int(layer["id"]) is { } id && Number(layer["color"]) is { } tint)
                tints[id] = (int)tint;
        }

        return tints;
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static double? Number(JsonNode? node) =>
        node is JsonValue value
            ? value.TryGetValue<double>(out var number)
                ? number
                : value.TryGetValue<string>(out var text)
                && double.TryParse(
                    text,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out number
                )
                    ? number
                    : null
            : null;

    private static int? Int(JsonNode? node) =>
        Number(node) is { } number && double.IsFinite(number) ? (int)Math.Truncate(number) : null;

    private sealed class Layer
    {
        public string Tag { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public double Alpha { get; set; } = 255;
        public string Ink { get; set; } = "normal";
    }

    private sealed record Frame(
        int X,
        int Y,
        int W,
        int H,
        int TrimX,
        int TrimY,
        int BoxW,
        int BoxH
    );

    private sealed record Asset(string Name, string Source, int X, int Y, bool FlipH, bool FlipV);

    private sealed record Part(
        int Order,
        Asset Asset,
        Frame Frame,
        int Left,
        int Top,
        Layer Layer,
        int Tint
    );

    /// <summary>An icon asset's name as a converter may have left it: <c>&lt;anything&gt;_icon_&lt;letter&gt;</c>, perhaps with a direction and frame.</summary>
    [GeneratedRegex(@"_icon_([a-z])(?:_\d{1,3}){0,2}$")]
    private static partial Regex LooseIconPattern();
}
