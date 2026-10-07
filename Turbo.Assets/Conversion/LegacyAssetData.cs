using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Turbo.Assets.Conversion;

/// <summary>
/// A prebuilt <c>.hab</c> library's JSON - the old Nitro layout, whose collections are objects
/// keyed by name or id - in the current layout, whose collections are arrays that carry the key.
/// Objects are walked in the order JavaScript gives their keys (index-like keys first, ascending),
/// as the studio's converter walked them.
/// </summary>
internal static class LegacyAssetData
{
    private static readonly HashSet<string> TAKEN =
    [
        "name",
        "type",
        "documentClass",
        "spritesheet",
        "assets",
        "aliases",
        "palettes",
        "visualizations",
        "animations",
    ];

    public sealed record Converted(
        JsonObject AssetData,
        JsonObject? Spritesheet,
        string? DocumentClass
    );

    public static Converted Convert(JsonObject legacy)
    {
        var data = new JsonObject();

        Set(data, "type", Copy(legacy["type"] ?? legacy["name"]));

        foreach (var (key, value) in Ordered(legacy))
            if (!TAKEN.Contains(key))
                data[key] = Copy(value);

        Set(data, "assets", ListOf(legacy["assets"], "name", numeric: false));
        Set(data, "aliases", ListOf(legacy["aliases"], "name", numeric: false));
        Set(data, "animations", ListOf(legacy["animations"], "name", numeric: false));
        Set(data, "palettes", ListOf(legacy["palettes"], "id"));
        Set(
            data,
            "visualizations",
            Map(
                ListOf(legacy["visualizations"]),
                v =>
                {
                    Set(v, "layers", ListOf(v["layers"], "id"));
                    Set(
                        v,
                        "colors",
                        Map(
                            ListOf(v["colors"], "id"),
                            c => Set(c, "layers", ListOf(c["layers"], "id"))
                        )
                    );
                    Set(
                        v,
                        "directions",
                        Map(
                            ListOf(v["directions"], "id"),
                            d => Set(d, "layers", ListOf(d["layers"], "id"))
                        )
                    );
                    Set(v, "animations", Map(ListOf(v["animations"], "id"), MapAnimation));
                }
            )
        );

        return new Converted(
            Clean(data),
            legacy["spritesheet"] is JsonObject sheet ? (JsonObject)sheet.DeepClone() : null,
            legacy["documentClass"]?.GetValue<string>()
        );
    }

    /// <summary>The frames named by asset, as the packer names them: the client's carry the document class.</summary>
    public static JsonObject Spritesheet(JsonObject spritesheet, string documentClass, string image)
    {
        var prefix = $"{documentClass}_";
        var frames = new JsonObject();

        if (spritesheet["frames"] is JsonObject source)
            foreach (var (key, frame) in Ordered(source))
                frames[
                    key.StartsWith(prefix, StringComparison.Ordinal) ? key[prefix.Length..] : key
                ] = Copy(frame);

        var output = new JsonObject();

        foreach (var (key, value) in Ordered(spritesheet))
            output[key] = key == "frames" ? frames : Copy(value);

        if (!output.ContainsKey("frames"))
            output["frames"] = frames;

        if (output["meta"] is JsonObject meta)
            meta["image"] = image;

        return output;
    }

    private static void MapAnimation(JsonObject animation) =>
        Set(
            animation,
            "layers",
            Map(
                ListOf(animation["layers"], "id"),
                layer =>
                    Set(
                        layer,
                        "frameSequences",
                        Map(
                            ListOf(layer["frameSequences"]),
                            sequence =>
                                Set(
                                    sequence,
                                    "frames",
                                    Map(
                                        ListOf(sequence["frames"]),
                                        frame => Set(frame, "offsets", ListOf(frame["offsets"]))
                                    )
                                )
                        )
                    )
            )
        );

    /// <summary>
    /// A keyed collection as an array: <c>{ "2": {...} }</c> is <c>[{ id: 2, ... }]</c>, the key as
    /// <paramref name="field"/> unless the item has it, numeric when <paramref name="numeric"/>. An
    /// array is taken as it is; null for nothing.
    /// </summary>
    private static JsonArray? ListOf(JsonNode? value, string? field = null, bool numeric = true)
    {
        switch (value)
        {
            case null:
                return null;
            case JsonArray array:
                return (JsonArray)array.DeepClone();
            case JsonObject keyed:
            {
                var list = new JsonArray();

                foreach (var (key, item) in Ordered(keyed))
                {
                    if (field is null || item is not JsonObject obj || obj.ContainsKey(field))
                    {
                        list.Add(Copy(item));

                        continue;
                    }

                    var withKey = new JsonObject
                    {
                        [field] = numeric
                            ? JsValues.Number(
                                double.TryParse(
                                    key,
                                    NumberStyles.Float,
                                    CultureInfo.InvariantCulture,
                                    out var n
                                )
                                    ? n
                                    : null
                            )
                            : key,
                    };

                    foreach (var (k, v) in Ordered(obj))
                        withKey[k] = Copy(v);

                    list.Add(withKey);
                }

                return list;
            }
            default:
                return null;
        }
    }

    private static JsonArray? Map(JsonArray? list, Action<JsonObject> map)
    {
        if (list is null)
            return null;

        foreach (var item in list.OfType<JsonObject>())
            map(item);

        return list;
    }

    /// <summary>A key set to a value, or gone when there is none - as JSON drops an undefined one.</summary>
    private static void Set(JsonObject target, string key, JsonNode? value)
    {
        if (value is null)
            target.Remove(key);
        else
            target[key] = value;
    }

    private static JsonObject Clean(JsonObject data) => (JsonObject)data.DeepClone();

    private static JsonNode? Copy(JsonNode? node) => node?.DeepClone();

    /// <summary>An object's keys in the order JavaScript gives them: index-like keys ascending, then the rest as written.</summary>
    private static IEnumerable<KeyValuePair<string, JsonNode?>> Ordered(JsonObject obj)
    {
        var indexed = obj.Where(x => IsIndex(x.Key))
            .OrderBy(x => uint.Parse(x.Key, CultureInfo.InvariantCulture));

        return indexed.Concat(obj.Where(x => !IsIndex(x.Key))).ToList();
    }

    private static bool IsIndex(string key) =>
        key.Length > 0
        && (key == "0" || key[0] != '0')
        && uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
        && value != uint.MaxValue;
}
