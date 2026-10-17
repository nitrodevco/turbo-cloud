using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Turbo.Assets.Nitro;

namespace Turbo.Gamedata.Assets;

/// <summary>
/// The client's figure and effect maps (FigureMap.json, EffectMap.json), built from the hotel's
/// clothing and effect bundles: each bundle's entries (<c>asset_bundles.map_entries</c>) as Habbo's
/// <c>figuremap.xml</c> and <c>effectmap.xml</c> list them, or - for a bundle of the hotel's own,
/// which no map lists - as its file holds them.
/// </summary>
internal static class ClientMaps
{
    /// <summary>The effect type of an effect the client wears; a dance is <c>dance</c>.</summary>
    public const string EFFECT_TYPE = "fx";

    private static readonly JsonSerializerOptions JSON = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>A clothing library's parts as <c>figuremap.xml</c> lists them (its <c>&lt;lib&gt;</c>s), each once, in its order.</summary>
    public static string FigureEntries(IEnumerable<XElement> libs) =>
        Write([
            .. libs.Elements("part")
                .Select(x =>
                    (
                        Id: ((string?)x.Attribute("id"))?.Trim() ?? string.Empty,
                        Type: ((string?)x.Attribute("type"))?.Trim() ?? string.Empty
                    )
                )
                .Where(x => x.Id.Length > 0 && x.Type.Length > 0)
                .DistinctBy(x => (x.Type, x.Id))
                .Select(x => Part(x.Id, x.Type)),
        ]);

    /// <summary>An effect library's effects as <c>effectmap.xml</c> lists them, each once, in its order.</summary>
    public static string EffectEntries(IEnumerable<XElement> effects) =>
        Write([
            .. effects
                .Select(x =>
                    (
                        Id: ((string?)x.Attribute("id"))?.Trim() ?? string.Empty,
                        Type: ((string?)x.Attribute("type"))?.Trim() ?? string.Empty
                    )
                )
                .Where(x => x.Id.Length > 0)
                .DistinctBy(x => x.Id)
                .Select(x => Effect(x.Id, x.Type.Length > 0 ? x.Type : EFFECT_TYPE)),
        ]);

    /// <summary>
    /// A clothing bundle's parts read from its file: every <c>&lt;size&gt;_&lt;action&gt;_&lt;type&gt;_&lt;id&gt;_&lt;direction&gt;_&lt;frame&gt;</c>
    /// asset. For a library no map lists; Habbo's own lists fewer than their files hold.
    /// </summary>
    public static string FigureEntriesOf(NitroBundle bundle, string name)
    {
        if (!bundle.Files.TryGetValue($"{name}.json", out var data))
            return "[]";

        var assets = JsonNode.Parse(data)?["assets"] switch
        {
            JsonObject byName => byName.Select(x => x.Key),
            JsonArray list => list.Select(x => x?["name"]?.GetValue<string>() ?? string.Empty),
            _ => [],
        };
        var parts = new List<JsonNode>();
        var seen = new HashSet<(string, int)>();

        foreach (var asset in assets)
        {
            var pieces = asset.Split('_');

            if (
                pieces.Length >= 6
                && pieces[0] is "h" or "sh"
                && pieces[2].Length > 0
                && int.TryParse(
                    pieces[3],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var id
                )
                && seen.Add((pieces[2], id))
            )
                parts.Add(new JsonObject { ["id"] = id, ["type"] = pieces[2] });
        }

        return Write(parts);
    }

    /// <summary>An effect bundle's effects from the ids that load it, each a worn effect.</summary>
    public static string EffectEntriesOf(IEnumerable<int> ids) =>
        Write([
            .. ids.Distinct()
                .Select(x => Effect(x.ToString(CultureInfo.InvariantCulture), EFFECT_TYPE)),
        ]);

    /// <summary>FigureMap.json: <c>libraries</c>, each with its revision and parts.</summary>
    public static byte[] WriteFigureMap(
        IEnumerable<(string Name, string? Revision, string Entries)> libraries
    )
    {
        var list = new JsonArray();

        foreach (var (name, revision, entries) in libraries)
        {
            var library = new JsonObject { ["id"] = name };

            if (Revision(revision) is { } number)
                library["revision"] = number;

            library["parts"] = Read(entries);
            list.Add(library);
        }

        return JsonSerializer.SerializeToUtf8Bytes(new JsonObject { ["libraries"] = list }, JSON);
    }

    /// <summary>EffectMap.json: <c>effects</c>, each with the library it is drawn from, its type and revision.</summary>
    public static byte[] WriteEffectMap(
        IEnumerable<(string Name, string? Revision, string Entries)> libraries
    )
    {
        var list = new JsonArray();

        foreach (var (name, revision, entries) in libraries)
        {
            foreach (var entry in Read(entries).OfType<JsonObject>())
            {
                var effect = new JsonObject
                {
                    ["id"] = entry["id"]?.DeepClone(),
                    ["lib"] = name,
                    ["type"] = entry["type"]?.DeepClone() ?? EFFECT_TYPE,
                };

                if (Revision(revision) is { } number)
                    effect["revision"] = number;

                list.Add(effect);
            }
        }

        return JsonSerializer.SerializeToUtf8Bytes(new JsonObject { ["effects"] = list }, JSON);
    }

    /// <summary>How many entries a library's map entries hold.</summary>
    public static int Count(string? entries) => entries is null ? 0 : Read(entries).Count;

    /// <summary>A part's id is a number, as the client's map has it, when it reads as one.</summary>
    private static JsonObject Part(string id, string type) =>
        new()
        {
            ["id"] = int.TryParse(
                id,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number
            )
                ? number
                : id,
            ["type"] = type,
        };

    private static JsonObject Effect(string id, string type) =>
        new() { ["id"] = id, ["type"] = type };

    private static int? Revision(string? revision) =>
        int.TryParse(revision, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;

    private static JsonArray Read(string entries)
    {
        try
        {
            return JsonNode.Parse(entries) as JsonArray ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Write(List<JsonNode> entries) =>
        JsonSerializer.Serialize(new JsonArray([.. entries]), JSON);
}
