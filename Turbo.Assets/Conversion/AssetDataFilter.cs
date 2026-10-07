using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Turbo.Assets.Conversion;

/// <summary>
/// What a bundle keeps of a library's asset data: the sizes the client draws
/// (<see cref="ALLOWED_SIZES"/>, and icons), no 32-pixel room textures, and no <c>sh_</c> shadows
/// for figure and effect libraries.
/// </summary>
public static class AssetDataFilter
{
    /// <summary>A figure library (clothing).</summary>
    public const string FIGURE = "figure";

    /// <summary>An avatar effect library.</summary>
    public const string EFFECT = "fx";

    public static readonly int[] ALLOWED_SIZES = [1, 64];

    private const string ROOM_TYPE = "room";

    public static void Apply(JsonObject data, string? assetType)
    {
        var type = data["type"]?.GetValue<string>() ?? string.Empty;
        var avatar = assetType is FIGURE or EFFECT;

        if (data["assets"] is JsonArray assets)
            foreach (var asset in assets.OfType<JsonObject>().ToList())
                if (!KeepAsset(asset["name"]?.GetValue<string>() ?? string.Empty, type, avatar))
                    assets.Remove(asset);

        if (data["visualizations"] is JsonArray visualizations)
            RemoveSizes(visualizations);

        if (data["logic"]?["particleSystems"] is JsonArray particles)
            RemoveSizes(particles);
    }

    private static bool KeepAsset(string name, string type, bool avatar)
    {
        if (avatar && name.StartsWith("sh_", StringComparison.Ordinal))
            return false;

        var rest = name.Length > type.Length ? name[(type.Length + 1)..] : string.Empty;
        var size = rest.Split('_')[0];

        if (
            type == ROOM_TYPE
            && (
                name.StartsWith("wall_texture_32", StringComparison.Ordinal)
                || name.StartsWith("floor_texture_32", StringComparison.Ordinal)
                || name.StartsWith("landscape_32", StringComparison.Ordinal)
                || name.EndsWith("_32", StringComparison.Ordinal)
                || name.EndsWith("_32_flipH", StringComparison.Ordinal)
            )
        )
            return false;

        if (size == "icon" || avatar)
            return true;

        return JsValues.ParseInt(size) is not { } number || ALLOWED_SIZES.Contains((int)number);
    }

    private static void RemoveSizes(JsonArray items)
    {
        foreach (var item in items.OfType<JsonObject>().ToList())
            if (
                item["size"] is JsonValue size
                && size.TryGetValue<double>(out var value)
                && !ALLOWED_SIZES.Contains((int)value)
            )
                items.Remove(item);
    }
}
