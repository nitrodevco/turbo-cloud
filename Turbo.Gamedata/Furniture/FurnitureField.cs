using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Turbo.Database.Entities.Furniture;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// One field of a furnidata item and the definition column that holds it. A value is compared as
/// the column holds it (<see cref="Normalize"/>): Habbo's <c>"1"</c> and the column's <c>1</c>
/// are the same value, and so are a height given to more decimals than its column keeps and the
/// rounded value the column holds.
/// </summary>
internal sealed class FurnitureField
{
    private readonly Func<FurnitureDefinitionEntity, JsonNode?> _read;
    private readonly Action<FurnitureDefinitionEntity, JsonNode?> _write;
    private readonly Func<JsonNode?, JsonNode?> _normalize;

    private FurnitureField(
        string key,
        bool floorOnly,
        bool clientOnly,
        int? maxLength,
        bool fromFile,
        Func<FurnitureDefinitionEntity, JsonNode?> read,
        Action<FurnitureDefinitionEntity, JsonNode?> write,
        Func<JsonNode?, JsonNode?> normalize
    )
    {
        Key = key;
        FloorOnly = floorOnly;
        ClientOnly = clientOnly;
        MaxLength = maxLength;
        FromFile = fromFile;
        _read = read;
        _write = write;
        _normalize = normalize;
    }

    /// <summary>The furnidata key.</summary>
    public string Key { get; }

    /// <summary>Only a floor item has it; Habbo's wall items leave it out.</summary>
    public bool FloorOnly { get; }

    /// <summary>
    /// Only the client's furnidata has it: a column that came with gamedata, which nothing in the
    /// hotel set before. The first import fills it from Habbo even where there is no earlier
    /// Habbo value to compare with.
    /// </summary>
    public bool ClientOnly { get; }

    /// <summary>The longest text the column holds.</summary>
    public int? MaxLength { get; }

    /// <summary>
    /// Furnidata does not have it; the furniture's asset file does (<c>states</c>). A Habbo item
    /// whose file was not read has no value for it, which says nothing - not that it is zero.
    /// </summary>
    public bool FromFile { get; }

    /// <summary>Whether this Habbo item says anything of the field.</summary>
    public bool IsIn(JsonObject habbo) => !FromFile || habbo.ContainsKey(Key);

    public JsonNode? Read(FurnitureDefinitionEntity definition) => _read(definition);

    public void Write(FurnitureDefinitionEntity definition, JsonNode? value) =>
        _write(definition, value);

    /// <summary>The value as the column would hold it, read back.</summary>
    public JsonNode? Normalize(JsonNode? value) => _normalize(value);

    public bool AppliesTo(bool isWall) => !(isWall && FloorOnly);

    public static bool Same(JsonNode? a, JsonNode? b) => Text(a) == Text(b);

    public static string Text(JsonNode? value) => value?.ToJsonString() ?? "null";

    public static FurnitureField Int(
        string key,
        Func<FurnitureDefinitionEntity, int> get,
        Action<FurnitureDefinitionEntity, int> set,
        bool floorOnly = false,
        bool clientOnly = false,
        bool fromFile = false
    ) =>
        new(
            key,
            floorOnly,
            clientOnly,
            null,
            fromFile,
            e => JsonValue.Create(get(e)),
            (e, v) => set(e, FurnitureValues.ToInt(v, key)),
            v => JsonValue.Create(FurnitureValues.ToInt(v, key))
        );

    public static FurnitureField Bool(
        string key,
        Func<FurnitureDefinitionEntity, bool> get,
        Action<FurnitureDefinitionEntity, bool> set,
        bool floorOnly = false,
        bool clientOnly = false
    ) =>
        new(
            key,
            floorOnly,
            clientOnly,
            null,
            false,
            e => JsonValue.Create(get(e)),
            (e, v) => set(e, FurnitureValues.ToBool(v, key)),
            v => JsonValue.Create(FurnitureValues.ToBool(v, key))
        );

    /// <summary>A number the column keeps to <paramref name="decimals"/> places.</summary>
    public static FurnitureField Double(
        string key,
        int decimals,
        Func<FurnitureDefinitionEntity, double> get,
        Action<FurnitureDefinitionEntity, double> set,
        bool floorOnly = false
    ) =>
        new(
            key,
            floorOnly,
            false,
            null,
            false,
            e => JsonValue.Create(Math.Round(get(e), decimals)),
            (e, v) => set(e, Math.Round(FurnitureValues.ToDouble(v, key), decimals)),
            v => JsonValue.Create(Math.Round(FurnitureValues.ToDouble(v, key), decimals))
        );

    public static FurnitureField Text(
        string key,
        int? maxLength,
        Func<FurnitureDefinitionEntity, string?> get,
        Action<FurnitureDefinitionEntity, string?> set,
        bool floorOnly = false
    ) =>
        new(
            key,
            floorOnly,
            true,
            maxLength,
            false,
            e => get(e) is { } text ? JsonValue.Create(text) : null,
            (e, v) => set(e, FurnitureValues.ToText(v, key)),
            v => FurnitureValues.ToText(v, key) is { } text ? JsonValue.Create(text) : null
        );

    public static FurnitureField Colors(
        string key,
        Func<FurnitureDefinitionEntity, List<string>?> get,
        Action<FurnitureDefinitionEntity, List<string>?> set
    ) =>
        new(
            key,
            true,
            true,
            null,
            false,
            e => FurnitureValues.FromColors(get(e)),
            (e, v) => set(e, FurnitureValues.ToColors(v, key)),
            v => FurnitureValues.FromColors(FurnitureValues.ToColors(v, key))
        );
}
