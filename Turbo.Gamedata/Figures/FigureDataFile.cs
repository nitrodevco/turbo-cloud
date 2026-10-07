using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata.Figures;

/// <summary>
/// Habbo's figure data read (<c>/gamedata/figuredata</c>, XML: <c>&lt;colors&gt;</c> of palettes and
/// <c>&lt;sets&gt;</c> of kinds of clothing, each with its pieces) as records in their one shape
/// (<see cref="FigureRecords"/>), and the hotel's written as the client loads it: FigureData.json,
/// <c>palettes</c> and <c>setTypes</c>, as the studio's converter writes it.
/// </summary>
internal static class FigureDataFile
{
    /// <summary>Habbo's order of the kinds of clothing; any other after them, by name.</summary>
    private static readonly string[] TYPE_ORDER =
    [
        "hr",
        "hd",
        "ch",
        "lg",
        "sh",
        "ha",
        "he",
        "ea",
        "fa",
        "ca",
        "wa",
        "cc",
        "cp",
    ];

    private static readonly JsonWriterOptions WRITER_OPTIONS = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Every record of the file, in its order. A record that doesn't read - a colour that isn't
    /// hex, a piece with no id - is left out, as the client would fail on it. Throws
    /// <see cref="FormatException"/> when it isn't figure data at all.
    /// </summary>
    public static List<(FigureRecordKind Kind, JsonObject Record)> Parse(byte[] xml)
    {
        XDocument document;

        try
        {
            using var stream = new MemoryStream(xml);
            using var reader = XmlReader.Create(
                stream,
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }
            );

            document = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new FormatException("The figure data is not XML.", ex);
        }

        var root = document.Root;

        if (root?.Name.LocalName != "figuredata")
            throw new FormatException("The figure data has no <figuredata>.");

        var records = new List<(FigureRecordKind, JsonObject)>();

        foreach (var palette in root.Elements("colors").Elements("palette"))
        foreach (var color in palette.Elements("color"))
            Add(
                records,
                FigureRecordKind.Color,
                new JsonObject
                {
                    [FigureRecords.PALETTE] = Attribute(palette, "id"),
                    [FigureRecords.ID] = Attribute(color, "id"),
                    [FigureRecords.INDEX] = Attribute(color, "index"),
                    [FigureRecords.CLUB] = Attribute(color, "club"),
                    [FigureRecords.SELECTABLE] = Attribute(color, "selectable"),
                    [FigureRecords.HEX] = color.Value.Trim(),
                }
            );

        foreach (var type in root.Elements("sets").Elements("settype"))
        {
            var name = Attribute(type, "type") ?? string.Empty;

            Add(
                records,
                FigureRecordKind.SetType,
                new JsonObject
                {
                    [FigureRecords.TYPE] = name,
                    [FigureRecords.PALETTE_ID] = Attribute(type, "paletteid"),
                    [FigureRecords.MAND_M_0] = Attribute(type, "mand_m_0"),
                    [FigureRecords.MAND_F_0] = Attribute(type, "mand_f_0"),
                    [FigureRecords.MAND_M_1] = Attribute(type, "mand_m_1"),
                    [FigureRecords.MAND_F_1] = Attribute(type, "mand_f_1"),
                }
            );

            foreach (var set in type.Elements("set"))
            {
                var parts = new JsonArray();

                foreach (var part in set.Elements("part"))
                {
                    var record = new JsonObject
                    {
                        [FigureRecords.ID] = Attribute(part, "id"),
                        [FigureRecords.TYPE] = Attribute(part, "type"),
                        [FigureRecords.COLORABLE] = Attribute(part, "colorable"),
                        [FigureRecords.INDEX] = Attribute(part, "index"),
                        [FigureRecords.COLOR_INDEX] = Attribute(part, "colorindex"),
                    };

                    if (Attribute(part, "palettemapid") is { Length: > 0 } map)
                        record[FigureRecords.PALETTE_MAP_ID] = map;

                    if (Attribute(part, "breed") is { Length: > 0 } breed)
                        record[FigureRecords.BREED] = breed;

                    parts.Add(record);
                }

                var hidden = new JsonArray();

                foreach (var layer in set.Elements("hiddenlayers").Elements("layer"))
                    hidden.Add(Attribute(layer, "parttype"));

                Add(
                    records,
                    FigureRecordKind.Set,
                    new JsonObject
                    {
                        [FigureRecords.ID] = Attribute(set, "id"),
                        [FigureRecords.TYPE] = name,
                        [FigureRecords.GENDER] = Attribute(set, "gender"),
                        [FigureRecords.CLUB] = Attribute(set, "club"),
                        [FigureRecords.COLORABLE] = Attribute(set, "colorable"),
                        [FigureRecords.SELECTABLE] = Attribute(set, "selectable"),
                        [FigureRecords.PRESELECTABLE] = Attribute(set, "preselectable"),
                        [FigureRecords.SELLABLE] = Attribute(set, "sellable"),
                        [FigureRecords.PARTS] = parts,
                        [FigureRecords.HIDDEN_LAYERS] = hidden,
                    }
                );
            }
        }

        return records;
    }

    /// <summary>
    /// FigureData.json from records in their one shape: palettes by id with their colours by
    /// index, kinds of clothing in Habbo's order with their pieces by id. A piece whose kind
    /// isn't listed is left out, as the client finds a piece only through its kind.
    /// </summary>
    public static byte[] Write(IEnumerable<(FigureRecordKind Kind, JsonObject Record)> records)
    {
        var all = records.ToList();
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream, WRITER_OPTIONS))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("palettes");

            foreach (
                var palette in all.Where(x => x.Kind == FigureRecordKind.Color)
                    .Select(x => x.Record)
                    .GroupBy(x => Int(x, FigureRecords.PALETTE))
                    .OrderBy(x => x.Key)
            )
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", palette.Key);
                writer.WriteStartArray("colors");

                foreach (
                    var color in palette
                        .OrderBy(x => Int(x, FigureRecords.INDEX))
                        .ThenBy(x => Int(x, FigureRecords.ID))
                )
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("id", Int(color, FigureRecords.ID));
                    writer.WriteNumber("index", Int(color, FigureRecords.INDEX));
                    writer.WriteNumber("club", Int(color, FigureRecords.CLUB));
                    writer.WriteBoolean("selectable", Bool(color, FigureRecords.SELECTABLE));
                    writer.WriteString("hexCode", Str(color, FigureRecords.HEX));
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("setTypes");

            var sets = all.Where(x => x.Kind == FigureRecordKind.Set)
                .Select(x => x.Record)
                .ToLookup(x => Str(x, FigureRecords.TYPE), StringComparer.Ordinal);

            foreach (
                var type in all.Where(x => x.Kind == FigureRecordKind.SetType)
                    .Select(x => x.Record)
                    .OrderBy(x => Order(Str(x, FigureRecords.TYPE)))
                    .ThenBy(x => Str(x, FigureRecords.TYPE), StringComparer.Ordinal)
            )
            {
                var name = Str(type, FigureRecords.TYPE);

                writer.WriteStartObject();
                writer.WriteString("type", name);
                writer.WriteNumber("paletteId", Int(type, FigureRecords.PALETTE_ID));
                writer.WriteBoolean("mandatory_f_0", Bool(type, FigureRecords.MAND_F_0));
                writer.WriteBoolean("mandatory_f_1", Bool(type, FigureRecords.MAND_F_1));
                writer.WriteBoolean("mandatory_m_0", Bool(type, FigureRecords.MAND_M_0));
                writer.WriteBoolean("mandatory_m_1", Bool(type, FigureRecords.MAND_M_1));

                var ofType = sets[name].OrderBy(x => Int(x, FigureRecords.ID)).ToList();

                if (ofType.Count > 0)
                {
                    writer.WriteStartArray("sets");

                    foreach (var set in ofType)
                        WriteSet(writer, set);

                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void WriteSet(Utf8JsonWriter writer, JsonObject set)
    {
        writer.WriteStartObject();
        writer.WriteNumber("id", Int(set, FigureRecords.ID));
        writer.WriteString("gender", Str(set, FigureRecords.GENDER));
        writer.WriteNumber("club", Int(set, FigureRecords.CLUB));
        writer.WriteBoolean("colorable", Bool(set, FigureRecords.COLORABLE));
        writer.WriteBoolean("selectable", Bool(set, FigureRecords.SELECTABLE));
        writer.WriteBoolean("preselectable", Bool(set, FigureRecords.PRESELECTABLE));
        writer.WriteBoolean("sellable", Bool(set, FigureRecords.SELLABLE));

        var parts = (set[FigureRecords.PARTS] as JsonArray ?? []).OfType<JsonObject>().ToList();

        if (parts.Count > 0)
        {
            writer.WriteStartArray("parts");

            foreach (var part in parts)
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", Int(part, FigureRecords.ID));
                writer.WriteString("type", Str(part, FigureRecords.TYPE));
                writer.WriteBoolean("colorable", Bool(part, FigureRecords.COLORABLE));
                writer.WriteNumber("index", Int(part, FigureRecords.INDEX));
                writer.WriteNumber("colorindex", Int(part, FigureRecords.COLOR_INDEX));

                if (part[FigureRecords.PALETTE_MAP_ID] is not null)
                    writer.WriteNumber("paletteMapId", Int(part, FigureRecords.PALETTE_MAP_ID));

                if (part[FigureRecords.BREED] is not null)
                    writer.WriteNumber("breed", Int(part, FigureRecords.BREED));

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        var hidden = (set[FigureRecords.HIDDEN_LAYERS] as JsonArray ?? []).ToList();

        if (hidden.Count > 0)
        {
            writer.WriteStartArray("hiddenLayers");

            foreach (var layer in hidden)
            {
                writer.WriteStartObject();
                writer.WriteString("partType", layer!.GetValue<string>());
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void Add(
        List<(FigureRecordKind, JsonObject)> records,
        FigureRecordKind kind,
        JsonObject raw
    )
    {
        try
        {
            records.Add((kind, FigureRecords.Normalize(kind, raw)));
        }
        catch (ArgumentException)
        {
            // Not a record the client could use either.
        }
    }

    /// <summary>An attribute's value; null when it has none, so the field takes its default.</summary>
    private static string? Attribute(XElement element, string name) =>
        element.Attribute(name)?.Value.Trim();

    /// <summary>Where a kind of clothing comes in Habbo's order; any other after them.</summary>
    public static int Order(string type)
    {
        var index = Array.IndexOf(TYPE_ORDER, type);

        return index < 0 ? TYPE_ORDER.Length : index;
    }

    private static int Int(JsonObject record, string field) => record[field]!.GetValue<int>();

    private static bool Bool(JsonObject record, string field) => record[field]!.GetValue<bool>();

    private static string Str(JsonObject record, string field) => record[field]!.GetValue<string>();
}
