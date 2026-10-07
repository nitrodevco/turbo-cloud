using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata.Figures;

/// <summary>
/// A figure record as JSON, by the names Habbo's file gives its attributes, always in one shape:
/// the same fields in the same order with the same types, so two records compare as text. A
/// colour is <c>{ palette, id, index, club, selectable, hex }</c>, a kind of clothing
/// <c>{ type, paletteid, mand_m_0, mand_f_0, mand_m_1, mand_f_1 }</c>, a piece
/// <c>{ id, type, gender, club, colorable, selectable, preselectable, sellable, parts, hiddenlayers }</c>
/// with each part <c>{ id, type, colorable, index, colorindex }</c> (and <c>palettemapid</c>,
/// <c>breed</c> when it has them) and each hidden layer a part type.
/// </summary>
internal static partial class FigureRecords
{
    public const string PALETTE = "palette";
    public const string ID = "id";
    public const string INDEX = "index";
    public const string CLUB = "club";
    public const string SELECTABLE = "selectable";
    public const string HEX = "hex";
    public const string TYPE = "type";
    public const string PALETTE_ID = "paletteid";
    public const string MAND_M_0 = "mand_m_0";
    public const string MAND_F_0 = "mand_f_0";
    public const string MAND_M_1 = "mand_m_1";
    public const string MAND_F_1 = "mand_f_1";
    public const string GENDER = "gender";
    public const string COLORABLE = "colorable";
    public const string PRESELECTABLE = "preselectable";
    public const string SELLABLE = "sellable";
    public const string PARTS = "parts";
    public const string HIDDEN_LAYERS = "hiddenlayers";
    public const string COLOR_INDEX = "colorindex";
    public const string PALETTE_MAP_ID = "palettemapid";
    public const string BREED = "breed";

    /// <summary>The highest club level the client knows: 0 none, 1 club, 2 VIP.</summary>
    public const int MAX_CLUB = 2;

    private static readonly string[] SET_TYPE_BOOLEANS = [MAND_M_0, MAND_F_0, MAND_M_1, MAND_F_1];

    private static readonly string[] SET_BOOLEANS =
    [
        COLORABLE,
        SELECTABLE,
        PRESELECTABLE,
        SELLABLE,
    ];

    /// <summary>The fields an import compares, besides those a record is known by.</summary>
    public static IReadOnlyList<string> Fields(FigureRecordKind kind) =>
        kind switch
        {
            FigureRecordKind.Color => [INDEX, CLUB, SELECTABLE, HEX],
            FigureRecordKind.SetType => [PALETTE_ID, .. SET_TYPE_BOOLEANS],
            _ => [TYPE, GENDER, CLUB, .. SET_BOOLEANS, PARTS, HIDDEN_LAYERS],
        };

    /// <summary>What a record is known by: a colour's <c>palette/id</c>, a kind's type, a piece's id.</summary>
    public static string KeyOf(FigureRecordKind kind, JsonObject record) =>
        kind switch
        {
            FigureRecordKind.Color => ColorKey(Int(record, PALETTE), Int(record, ID)),
            FigureRecordKind.SetType => Str(record, TYPE),
            _ => Int(record, ID).ToString(CultureInfo.InvariantCulture),
        };

    /// <summary>What a record is listed under: a colour's palette, a kind's own type, a piece's kind.</summary>
    public static string GroupOf(FigureRecordKind kind, JsonObject record) =>
        kind switch
        {
            FigureRecordKind.Color => Int(record, PALETTE).ToString(CultureInfo.InvariantCulture),
            _ => Str(record, TYPE),
        };

    public static string ColorKey(int palette, int id) =>
        string.Create(CultureInfo.InvariantCulture, $"{palette}/{id}");

    /// <summary>
    /// The record in its one shape. Throws <see cref="ArgumentException"/> for fields that don't
    /// make one: a missing or mistyped field, a type that isn't letters, a colour that isn't hex.
    /// Booleans may be given as Habbo's file gives them, <c>0</c> and <c>1</c>.
    /// </summary>
    public static JsonObject Normalize(FigureRecordKind kind, JsonObject input) =>
        kind switch
        {
            FigureRecordKind.Color => new JsonObject
            {
                [PALETTE] = NonNegative(input, PALETTE),
                [ID] = NonNegative(input, ID),
                [INDEX] = IntOr(input, INDEX, 0),
                [CLUB] = Club(input),
                [SELECTABLE] = Bool(input, SELECTABLE),
                [HEX] = Hex(input),
            },
            FigureRecordKind.SetType => SetType(input),
            FigureRecordKind.Set => Set(input),
            _ => throw new ArgumentException($"There is no figure record kind {kind}."),
        };

    /// <summary>A record's fields as text, to compare and store.</summary>
    public static string Json(JsonObject record) => record.ToJsonString();

    /// <summary>One field's value as text, to compare: null when the record has none.</summary>
    public static string FieldJson(JsonObject? record, string field) =>
        record?[field]?.ToJsonString() ?? "null";

    public static JsonObject Parse(string json) =>
        JsonNode.Parse(json) as JsonObject
        ?? throw new ArgumentException("A figure record is a JSON object.");

    private static JsonObject SetType(JsonObject input)
    {
        var record = new JsonObject
        {
            [TYPE] = PartType(input, TYPE),
            [PALETTE_ID] = NonNegative(input, PALETTE_ID),
        };

        foreach (var field in SET_TYPE_BOOLEANS)
            record[field] = Bool(input, field);

        return record;
    }

    private static JsonObject Set(JsonObject input)
    {
        var gender = Str(input, GENDER).ToUpperInvariant();

        if (gender is not ("M" or "F" or "U"))
            throw new ArgumentException($"A piece's gender is M, F or U, not {gender}.");

        var record = new JsonObject
        {
            [ID] = NonNegative(input, ID),
            [TYPE] = PartType(input, TYPE),
            [GENDER] = gender,
            [CLUB] = Club(input),
        };

        foreach (var field in SET_BOOLEANS)
            record[field] = Bool(input, field);

        var parts = new JsonArray();

        foreach (var node in input[PARTS] as JsonArray ?? [])
        {
            if (node is not JsonObject part)
                throw new ArgumentException("Each part is an object.");

            var normal = new JsonObject
            {
                [ID] = NonNegative(part, ID),
                [TYPE] = PartType(part, TYPE),
                [COLORABLE] = Bool(part, COLORABLE),
                [INDEX] = IntOr(part, INDEX, 0),
                [COLOR_INDEX] = IntOr(part, COLOR_INDEX, 0),
            };

            // Only pets' parts have these; Habbo's file leaves them out of everyone else's.
            if (part[PALETTE_MAP_ID] is not null)
                normal[PALETTE_MAP_ID] = Int(part, PALETTE_MAP_ID);

            if (part[BREED] is not null)
                normal[BREED] = Int(part, BREED);

            parts.Add(normal);
        }

        record[PARTS] = parts;

        var hidden = new JsonArray();

        foreach (var node in input[HIDDEN_LAYERS] as JsonArray ?? [])
            hidden.Add(
                PartTypeOf(
                    node is JsonValue value && value.TryGetValue<string>(out var text)
                        ? text
                        : throw new ArgumentException("Each hidden layer is a part type.")
                )
            );

        record[HIDDEN_LAYERS] = hidden;

        return record;
    }

    private static int Club(JsonObject input)
    {
        var club = IntOr(input, CLUB, 0);

        return club is >= 0 and <= MAX_CLUB
            ? club
            : throw new ArgumentException($"A club level is 0 to {MAX_CLUB}, not {club}.");
    }

    private static string Hex(JsonObject input)
    {
        var hex = Str(input, HEX).TrimStart('#');

        return HexPattern().IsMatch(hex)
            ? hex
            : throw new ArgumentException($"A colour is six hex digits, not {hex}.");
    }

    private static string PartType(JsonObject input, string field) => PartTypeOf(Str(input, field));

    private static string PartTypeOf(string type) =>
        PartTypePattern().IsMatch(type)
            ? type
            : throw new ArgumentException(
                $"A part type is one to eight lowercase letters and digits, not {type}."
            );

    private static int NonNegative(JsonObject input, string field)
    {
        var value = Int(input, field);

        return value >= 0 ? value : throw new ArgumentException($"{field} can't be negative.");
    }

    private static int IntOr(JsonObject input, string field, int fallback) =>
        input[field] is null ? fallback : Int(input, field);

    private static int Int(JsonObject input, string field)
    {
        if (input[field] is JsonValue value)
        {
            if (value.TryGetValue<int>(out var number))
                return number;

            if (
                value.TryGetValue<string>(out var text)
                && int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out number
                )
            )
                return number;

            if (
                value.GetValueKind() == JsonValueKind.Number
                && value.TryGetValue<double>(out var real)
                && real == Math.Floor(real)
                && real is >= int.MinValue and <= int.MaxValue
            )
                return (int)real;
        }

        throw new ArgumentException($"{field} is a whole number.");
    }

    private static bool Bool(JsonObject input, string field)
    {
        switch (input[field])
        {
            case null:
                return false;
            case JsonValue value when value.TryGetValue<bool>(out var flag):
                return flag;
            case JsonValue value when value.TryGetValue<int>(out var number):
                return number != 0;
            case JsonValue value when value.TryGetValue<string>(out var text):
                return text is "1" or "true";
            default:
                throw new ArgumentException($"{field} is true or false.");
        }
    }

    private static string Str(JsonObject input, string field) =>
        input[field] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text.Trim()
            : throw new ArgumentException($"{field} is text.");

    /// <summary>The fields of a record, each as text: what a field-by-field comparison reads.</summary>
    public static Dictionary<string, string> FieldTexts(FigureRecordKind kind, JsonObject record) =>
        Fields(kind).ToDictionary(x => x, x => FieldJson(record, x), StringComparer.Ordinal);

    [GeneratedRegex("^[0-9A-Fa-f]{6}$")]
    private static partial Regex HexPattern();

    [GeneratedRegex("^[a-z0-9]{1,8}$")]
    private static partial Regex PartTypePattern();
}
