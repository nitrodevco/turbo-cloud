using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Turbo.Assets;
using Turbo.Assets.Conversion;
using Turbo.Assets.Nitro;

namespace Turbo.Gamedata.HandItems;

/// <summary>A single decoded local atlas, discarded after its catalogue previews are cropped.</summary>
internal sealed class HandItemPreview
{
    // Standalone item art should not allocate an atlas-sized output on malformed frame metadata.
    private const int MAX_PREVIEW_SIDE = 1024;
    private readonly Dictionary<string, JsonObject> _assets;
    private readonly JsonObject? _frames;
    private readonly RgbaImage? _sheet;

    public HandItemPreview(NitroBundle bundle, string library)
    {
        var data = bundle.AssetData(library) as JsonObject;
        if (
            data is null
            || data["assets"] is not JsonArray assets
            || (data["aliases"] is not null && data["aliases"] is not JsonArray)
        )
            throw new AssetFormatException("The hand-item manifest has invalid assets or aliases.");
        _assets = assets
            .Concat(data["aliases"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(x => HandItemCatalog.Text(x["name"]) is not null)
            .GroupBy(x => HandItemCatalog.Text(x["name"])!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        if (bundle.Files.TryGetValue(library + "_spritesheet.json", out var frames))
        {
            if (
                JsonNode.Parse(frames) is not JsonObject sheet
                || sheet["frames"] is not JsonObject entries
                || entries.Any(x =>
                    x.Value is not JsonObject entry || entry["frame"] is not JsonObject
                )
            )
                throw new AssetFormatException("The hand-item spritesheet has invalid frames.");
            _frames = entries;
        }
        if (bundle.Files.TryGetValue(library + ".png", out var png))
            _sheet = RgbaImage.Decode(png);
    }

    public bool Contains(string definition, int part) =>
        _sheet is not null && _frames is not null && Asset(definition, part) is not null;

    private string? Asset(string definition, int part)
    {
        var prefix = "h_" + definition + "_" + part.ToString(CultureInfo.InvariantCulture) + "_";
        var preferred = prefix + "2_0";
        return _assets.ContainsKey(preferred)
            ? preferred
            : _assets
                .Keys.Where(x => x.StartsWith(prefix, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .FirstOrDefault();
    }

    public byte[]? Draw(string definition, int part)
    {
        var name = Asset(definition, part);
        if (name is null || _sheet is null || _frames is null)
            return null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var flipH = false;
        var flipV = false;
        while (_assets.TryGetValue(name, out var asset))
        {
            if (!seen.Add(name))
                return null;
            flipH ^= asset["flipH"]?.ToString() == "true";
            flipV ^= asset["flipV"]?.ToString() == "true";
            if (
                (HandItemCatalog.Text(asset["source"]) ?? HandItemCatalog.Text(asset["link"]))
                    is not { Length: > 0 } source
                || source == name
            )
                break;
            name = source;
        }
        if (
            _frames[name] is not JsonObject entry
            || entry["frame"] is not JsonObject frame
            || entry["rotated"]?.ToString() == "true"
        )
            return null;
        var x = Number(frame["x"]);
        var y = Number(frame["y"]);
        var width = Number(frame["w"]);
        var height = Number(frame["h"]);
        var boxWidth = Number(entry["sourceSize"]?["w"], width);
        var boxHeight = Number(entry["sourceSize"]?["h"], height);
        var atX = Number(entry["spriteSourceSize"]?["x"]);
        var atY = Number(entry["spriteSourceSize"]?["y"]);
        if (
            x < 0
            || y < 0
            || width <= 0
            || height <= 0
            || width > _sheet.Width
            || height > _sheet.Height
            || x > _sheet.Width - width
            || y > _sheet.Height - height
            || boxWidth <= 0
            || boxHeight <= 0
            || boxWidth > MAX_PREVIEW_SIDE
            || boxHeight > MAX_PREVIEW_SIDE
            || atX < 0
            || atY < 0
            || width > boxWidth
            || height > boxHeight
            || atX > boxWidth - width
            || atY > boxHeight - height
        )
            return null;
        var crop = _sheet.Crop(x, y, width, height, boxWidth, boxHeight, atX, atY);
        if (!flipH && !flipV)
            return crop.EncodePng();
        var pixels = new byte[crop.Pixels.Length];
        for (var row = 0; row < boxHeight; row++)
        for (var column = 0; column < boxWidth; column++)
            Array.Copy(
                crop.Pixels,
                ((row * boxWidth) + column) * 4,
                pixels,
                (
                    ((flipV ? boxHeight - row - 1 : row) * boxWidth)
                    + (flipH ? boxWidth - column - 1 : column)
                ) * 4,
                4
            );
        return new RgbaImage(boxWidth, boxHeight, pixels).EncodePng();
    }

    private static int Number(JsonNode? node, int fallback = 0) =>
        node is null ? fallback
        : int.TryParse(
            node.ToString(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value
        )
            ? value
        : -1;
}
