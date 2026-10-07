using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// Furnidata values read as a column holds them. Habbo's file is not strict about kinds (a flag
/// may be <c>true</c> or <c>1</c>, a number may be a string, a text may be <c>null</c>), so each
/// reader takes what Habbo writes; anything else is an <see cref="ArgumentException"/>.
/// </summary>
internal static class FurnitureValues
{
    public static int ToInt(JsonNode? value, string key) =>
        Kind(value) switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => 0,
            JsonValueKind.Number => Number(value!) is var d
            && d is >= int.MinValue and <= int.MaxValue
                ? (int)d
                : throw Wrong(key, value, "a whole number"),
            JsonValueKind.String => int.TryParse(
                value!.GetValue<string>(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed
            )
                ? parsed
                : throw Wrong(key, value, "a whole number"),
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => throw Wrong(key, value, "a whole number"),
        };

    public static double ToDouble(JsonNode? value, string key) =>
        Kind(value) switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => 0,
            JsonValueKind.Number => Number(value!),
            JsonValueKind.String => double.TryParse(
                value!.GetValue<string>(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed
            )
                ? parsed
                : throw Wrong(key, value, "a number"),
            _ => throw Wrong(key, value, "a number"),
        };

    public static bool ToBool(JsonNode? value, string key) =>
        Kind(value) switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => false,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => Number(value!) != 0,
            JsonValueKind.String => value!.GetValue<string>().Trim().ToLowerInvariant() switch
            {
                "1" or "true" => true,
                "0" or "false" or "" => false,
                _ => throw Wrong(key, value, "true or false"),
            },
            _ => throw Wrong(key, value, "true or false"),
        };

    /// <summary>A text, or null; a number or a flag is written as JSON writes it.</summary>
    public static string? ToText(JsonNode? value, string key) =>
        Kind(value) switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => null,
            JsonValueKind.String => value!.GetValue<string>(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False =>
                value!.ToJsonString(),
            _ => throw Wrong(key, value, "a text"),
        };

    /// <summary>
    /// <c>partcolors</c>: <c>{ "color": [ ... ] }</c> as furnidata writes it, or the list on its
    /// own. None is null, as Habbo leaves the key out of an item without colours.
    /// </summary>
    public static List<string>? ToColors(JsonNode? value, string key)
    {
        var list = value switch
        {
            null => null,
            JsonObject obj => obj["color"] as JsonArray,
            JsonArray array => array,
            JsonValue v when v.GetValueKind() == JsonValueKind.Null => null,
            _ => throw Wrong(key, value, "a list of colours"),
        };

        if (list is null || list.Count == 0)
            return null;

        var colors = new List<string>(list.Count);

        foreach (var color in list)
            colors.Add(ToText(color, key) ?? throw Wrong(key, value, "a list of colours"));

        return colors;
    }

    public static JsonNode? FromColors(List<string>? colors)
    {
        if (colors is null || colors.Count == 0)
            return null;

        var list = new JsonArray();

        foreach (var color in colors)
            list.Add(color);

        return new JsonObject { ["color"] = list };
    }

    // A number parsed from a file and one made in code (JsonValue<int>) convert differently;
    // as JSON text they read alike.
    private static double Number(JsonNode value) =>
        double.Parse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static JsonValueKind Kind(JsonNode? value) =>
        value?.GetValueKind() ?? JsonValueKind.Undefined;

    private static ArgumentException Wrong(string key, JsonNode? value, string expected) =>
        new($"{key} must be {expected}, not {value?.ToJsonString() ?? "null"}.", key);
}
