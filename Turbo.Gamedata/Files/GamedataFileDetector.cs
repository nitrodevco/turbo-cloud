using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata.Files;

/// <summary>
/// Which gamedata file an upload is, by what it holds rather than by its name, in each form the
/// client has kept it:
/// <list type="bullet">
/// <item>JSON: furniture data (<c>roomitemtypes</c>), product data (<c>productdata</c>), figure data
/// (<c>palettes</c> beside <c>setTypes</c>), the figure map (<c>libraries</c>), the effect map
/// (<c>effects</c>), and a flat object of settings or of texts;</item>
/// <item>XML: <c>&lt;furnidata&gt;</c>, <c>&lt;figuredata&gt;</c>, and a <c>&lt;map&gt;</c> of
/// <c>&lt;lib&gt;</c> (the figure map) or of <c>&lt;effect&gt;</c> (the effect map);</item>
/// <item>text: the product data's <c>[[...]]</c> lines, and <c>key=value</c> lines of settings or
/// of texts.</item>
/// </list>
/// Settings and texts are both keys and values; settings are told apart by the keys a client
/// config always has, or by values that are not all words.
/// </summary>
internal static class GamedataFileDetector
{
    /// <summary>Keys a client's variables or a Nitro client config has, and texts never do.</summary>
    private static readonly HashSet<string> VARIABLE_KEYS = new(StringComparer.Ordinal)
    {
        "flash.client.url",
        "external.texts.txt",
        "external.variables.txt",
        "external.figurepartlist.txt",
        "furnidata.load.url",
        "productdata.load.url",
        "socket.url",
        "asset.url",
        "image.library.url",
        "hof.furni.url",
        "external.texts.url",
        "furnidata.url",
        "productdata.url",
        "figuredata.url",
        "nitro.config.url",
    };

    private static readonly JsonDocumentOptions JSON_OPTIONS = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = 64,
    };

    private static readonly XmlReaderSettings XML_SETTINGS = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreWhitespace = true,
    };

    // A file whose head has a NUL byte is binary, not one of these.
    private const int BINARY_SNIFF_BYTES = 1024;

    public static GamedataFileKind? Detect(byte[] data)
    {
        if (data.AsSpan(0, Math.Min(data.Length, BINARY_SNIFF_BYTES)).Contains((byte)0))
            return null;

        string text;

        try
        {
            text = new UTF8Encoding(false, true).GetString(data).TrimStart('\uFEFF').Trim();
        }
        catch (DecoderFallbackException)
        {
            return null;
        }

        if (text.Length == 0)
            return null;

        return text[0] switch
        {
            '{' => FromJson(text),
            '[' => text.StartsWith("[[", StringComparison.Ordinal)
                ? GamedataFileKind.ProductData
                : null,
            '<' => FromXml(text),
            _ => FromLines(text),
        };
    }

    private static GamedataFileKind? FromJson(string text)
    {
        JsonObject? root;

        try
        {
            root = JsonNode.Parse(text, documentOptions: JSON_OPTIONS) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }

        if (root is null)
            return null;

        if (root.ContainsKey("roomitemtypes") || root.ContainsKey("wallitemtypes"))
            return GamedataFileKind.FurnitureData;

        if (root.ContainsKey("productdata"))
            return GamedataFileKind.ProductData;

        if (
            root.ContainsKey("palettes")
            && (root.ContainsKey("setTypes") || root.ContainsKey("settypes"))
        )
            return GamedataFileKind.FigureData;

        if (root["libraries"] is JsonArray)
            return GamedataFileKind.FigureMap;

        if (root["effects"] is JsonArray)
            return GamedataFileKind.EffectMap;

        if (root.Count == 0 || root.Any(x => x.Value is JsonObject))
            return null;

        return
            root.Any(x => VARIABLE_KEYS.Contains(x.Key))
            || root.Any(x =>
                x.Value is not JsonValue value || value.GetValueKind() != JsonValueKind.String
            )
            ? GamedataFileKind.ExternalVariables
            : GamedataFileKind.ExternalTexts;
    }

    private static GamedataFileKind? FromXml(string text)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(text), XML_SETTINGS);

            if (reader.MoveToContent() != XmlNodeType.Element)
                return null;

            switch (reader.LocalName)
            {
                case "furnidata":
                    return GamedataFileKind.FurnitureData;
                case "figuredata":
                    return GamedataFileKind.FigureData;
                case "map":
                    // The map's first entry says which map it is.
                    while (reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element)
                            continue;

                        return reader.LocalName switch
                        {
                            "lib" => GamedataFileKind.FigureMap,
                            "effect" => GamedataFileKind.EffectMap,
                            _ => null,
                        };
                    }

                    return null;
                default:
                    return null;
            }
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static GamedataFileKind? FromLines(string text)
    {
        var lines = text.Split('\n')
            .Select(x => x.Trim())
            .Where(x => x.Length > 0 && !x.StartsWith('#'))
            .ToList();
        var keys = lines
            .Select(x => x.IndexOf('=', StringComparison.Ordinal))
            .Zip(lines, (at, line) => at > 0 ? line[..at].Trim() : null)
            .ToList();

        // Most lines must be key=value; a value may run on over lines of its own.
        if (keys.Count(x => x is not null) * 2 < lines.Count)
            return null;

        return keys.Any(x => x is not null && VARIABLE_KEYS.Contains(x))
            ? GamedataFileKind.ExternalVariables
            : GamedataFileKind.ExternalTexts;
    }
}
