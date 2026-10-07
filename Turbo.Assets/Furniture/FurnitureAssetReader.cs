using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using Turbo.Assets.Hab;
using Turbo.Assets.Nitro;
using Turbo.Assets.Swf;

namespace Turbo.Assets.Furniture;

/// <summary>
/// A furniture's asset file read for what it says of the furniture (<see cref="FurnitureAssetInfo"/>):
/// an SWF or a <c>.hab</c> through its XML documents (<c>index</c>, <c>&lt;type&gt;_logic</c>,
/// <c>&lt;type&gt;_visualization</c>), a <c>.nitro</c> through its asset data.
/// </summary>
public static class FurnitureAssetReader
{
    // A Habbo document is plain XML: nothing in it needs a DTD, and one could make a parser
    // fetch or expand what it names.
    private const int FIRST_SPECIAL_ANIMATION = 100;

    private static readonly XmlReaderSettings XML_SETTINGS = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
    };

    /// <summary>
    /// Reads a file of any of the three kinds, told apart by its first bytes. <paramref name="name"/>
    /// is the library's name, which a <c>.hab</c> or a bundle with several files may need.
    /// </summary>
    public static FurnitureAssetInfo Read(byte[] file, string? name = null)
    {
        if (SwfLibrary.IsSwf(file))
            return FromLibrary(SwfLibrary.Read(file));

        if (HabLibrary.IsHab(file))
            return FromLibrary(HabLibrary.Read(file, name));

        if (NitroBundle.IsNitro(file))
            return FromAssetData(
                NitroBundle.Read(file).AssetData(name)
                    ?? throw new AssetFormatException("The .nitro bundle has no asset data.")
            );

        throw new AssetFormatException("The file is not an SWF, a .hab or a .nitro bundle.");
    }

    public static FurnitureAssetInfo FromLibrary(IAssetLibrary library)
    {
        var index = Parse(library.GetXml("index"), "index");
        var type = Attribute(index, "type") ?? library.DocumentClass;
        var logic = Parse(library.GetXml($"{type}_logic"), "logic");
        var visualization = Parse(library.GetXml($"{type}_visualization"), "visualization");
        var model = logic?.Element("model");
        var dimensions = model?.Element("dimensions");
        var visualizations =
            visualization?.Element("graphics")?.Elements("visualization").ToList() ?? [];
        var animations = visualizations
            .SelectMany(x => x.Element("animations")?.Elements("animation") ?? [])
            .Select(x =>
                (
                    Id: Int(Attribute(x, "id")),
                    Transition: !string.IsNullOrEmpty(Attribute(x, "transitionTo"))
                        || !string.IsNullOrEmpty(Attribute(x, "transitionFrom"))
                )
            );
        var largest = visualizations.MaxBy(x => Int(Attribute(x, "size")) ?? 0);

        return Build(
            type,
            Attribute(index, "logic"),
            Attribute(index, "visualization"),
            animations,
            Number(Attribute(dimensions, "x")),
            Number(Attribute(dimensions, "y")),
            Number(Attribute(dimensions, "z")),
            model?.Element("directions")?.Elements("direction").Select(x => Int(Attribute(x, "id")))
                ?? [],
            visualizations.SelectMany(x =>
                x.Element("colors")?.Elements("color").Select(c => Int(Attribute(c, "id"))) ?? []
            ),
            Int(Attribute(largest, "layerCount")),
            visualizations.Select(x => Int(Attribute(x, "size")))
        );
    }

    /// <summary>
    /// A bundle's asset data. Its lists (colours, directions, animations) are arrays of objects
    /// with an <c>id</c>; a bundle of the old Nitro layout keyed them by id instead, and both read.
    /// </summary>
    public static FurnitureAssetInfo FromAssetData(JsonObject data)
    {
        var type = Text(data["type"]) ?? string.Empty;
        var model = data["logic"]?["model"];
        var dimensions = model?["dimensions"];
        var visualizations =
            (data["visualizations"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        var animations = visualizations
            .SelectMany(x => Items(x["animations"]))
            .Select(x =>
                (
                    Id: Int(Text(x.Value["id"]) ?? x.Key),
                    Transition: x.Value["transitionTo"] is not null
                        || x.Value["transitionFrom"] is not null
                )
            );
        var largest = visualizations.MaxBy(x => Int(Text(x["size"])) ?? 0);

        return Build(
            type,
            Text(data["logicType"]),
            Text(data["visualizationType"]),
            animations,
            Number(Text(dimensions?["x"])),
            Number(Text(dimensions?["y"])),
            Number(Text(dimensions?["z"])),
            (model?["directions"] as JsonArray)?.Select(x => Int(Text(x))) ?? [],
            visualizations.SelectMany(x =>
                Items(x["colors"]).Select(c => Int(Text(c.Value["id"]) ?? c.Key))
            ),
            Int(Text(largest?["layerCount"])),
            visualizations.Select(x => Int(Text(x["size"])))
        );
    }

    private static FurnitureAssetInfo Build(
        string type,
        string? logic,
        string? visualization,
        IEnumerable<(int? Id, bool Transition)> animations,
        double? x,
        double? y,
        double? z,
        IEnumerable<int?> directions,
        IEnumerable<int?> colors,
        int? layerCount,
        IEnumerable<int?> sizes
    )
    {
        var all = animations.Where(a => a.Id is not null).ToList();
        var states = Distinct(all.Where(a => IsState(a.Id!.Value, a.Transition)).Select(a => a.Id));
        var others = Distinct(
            all.Where(a => !IsState(a.Id!.Value, a.Transition)).Select(a => a.Id)
        );
        var highest = states.DefaultIfEmpty(-1).Max();

        return new FurnitureAssetInfo
        {
            Type = type,
            Logic = logic,
            Visualization = visualization,
            States = highest + 1,
            StateAnimations = states,
            OtherAnimations = others,
            DimensionX = x,
            DimensionY = y,
            DimensionZ = z,
            Directions = Distinct(directions),
            Colors = Distinct(colors),
            LayerCount = layerCount,
            Sizes = Distinct(sizes),
        };
    }

    /// <summary>
    /// Habbo numbers a furniture's states from 0, and its other animations apart: a transition
    /// between two states from 100 (and says so with <c>transitionTo</c> or <c>transitionFrom</c>),
    /// a special one below 0 (a dice rolling, -1).
    /// </summary>
    private static bool IsState(int id, bool transition) =>
        !transition && id is >= 0 and < FIRST_SPECIAL_ANIMATION;

    private static List<int> Distinct(IEnumerable<int?> values) =>
        [.. values.OfType<int>().Distinct().Order()];

    /// <summary>A list of objects, from an array or from an object keyed by id.</summary>
    private static IEnumerable<KeyValuePair<string?, JsonObject>> Items(JsonNode? node) =>
        node switch
        {
            JsonArray array => array
                .OfType<JsonObject>()
                .Select(x => new KeyValuePair<string?, JsonObject>(null, x)),
            JsonObject obj => obj.Where(x => x.Value is JsonObject)
                .Select(x => new KeyValuePair<string?, JsonObject>(x.Key, (JsonObject)x.Value!)),
            _ => [],
        };

    private static XElement? Parse(string? xml, string what)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), XML_SETTINGS);

            return XDocument.Load(reader).Root;
        }
        catch (XmlException ex)
        {
            throw new AssetFormatException(
                $"The library's {what} document is not readable XML.",
                ex
            );
        }
    }

    private static string? Attribute(XElement? element, string name) =>
        element?.Attribute(name)?.Value;

    private static string? Text(JsonNode? node) =>
        node switch
        {
            null => null,
            JsonValue value when value.GetValueKind() == JsonValueKind.String =>
                value.GetValue<string>(),
            JsonValue value => value.ToJsonString(),
            _ => null,
        };

    private static int? Int(string? text) =>
        Number(text) is { } number ? (int)Math.Round(number) : null;

    // "Infinity" and "NaN" parse, and some of Habbo's files say them; neither is a size.
    private static double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
        && double.IsFinite(number)
            ? number
            : null;
}
