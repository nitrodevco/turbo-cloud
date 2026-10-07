using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Turbo.Assets.Hab;
using Turbo.Assets.Nitro;
using Turbo.Assets.Swf;

namespace Turbo.Assets.Conversion;

/// <summary>
/// Habbo's asset libraries as the <c>.nitro</c> bundles the client loads - the studio's converter,
/// in C#:
/// <list type="bullet">
/// <item>an SWF, or a <c>.hab</c> carrying the SWF's documents: its documents mapped
/// (<see cref="AssetDataMapper"/>), filtered to the sizes the client draws
/// (<see cref="AssetDataFilter"/>), and the images its assets use packed into one sheet
/// (<see cref="SpriteSheetPacker"/>);</item>
/// <item>a prebuilt <c>.hab</c> (the small generic libraries): its packed sheet kept, its JSON brought
/// to the current layout (<see cref="LegacyAssetData"/>);</item>
/// <item>a <c>.nitro</c>: taken as it is.</item>
/// </list>
/// </summary>
public static class NitroConverter
{
    private static readonly JsonSerializerOptions JSON = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// The file as a bundle. <paramref name="name"/> names a <c>.hab</c>'s library as the client asks
    /// for it, which the file may not; <paramref name="assetType"/> is <see cref="AssetDataFilter.FIGURE"/>
    /// or <see cref="AssetDataFilter.EFFECT"/> for avatar libraries.
    /// </summary>
    public static NitroBundle Convert(byte[] file, string? name = null, string? assetType = null)
    {
        if (SwfLibrary.IsSwf(file))
        {
            var swf = SwfLibrary.Read(file);

            return FromLibrary(swf, ImageBundle.FromSwf(swf), assetType);
        }

        if (HabLibrary.IsHab(file))
        {
            var hab = HabLibrary.Read(file, name);

            return Prebuilt(hab, name, assetType)
                ?? FromLibrary(hab, ImageBundle.FromHab(hab), assetType);
        }

        if (NitroBundle.IsNitro(file))
            return NitroBundle.Read(file);

        throw new AssetFormatException("The file is not an SWF, a .hab or a .nitro bundle.");
    }

    private static NitroBundle FromLibrary(
        IAssetLibrary library,
        ImageBundle images,
        string? assetType
    )
    {
        var data = AssetDataMapper.Map(library);
        var name = library.DocumentClass;

        AssetDataFilter.Apply(data, assetType);
        Reference(data, images);

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [$"{name}.json"] = Json(data),
        };

        if (SpriteSheetPacker.Pack(images, name) is { } sheet)
        {
            files[$"{name}.png"] = sheet.Sheet.EncodePng();
            files[$"{name}_spritesheet.json"] = Json(sheet.Frames);
        }

        return NitroBundle.Create(files);
    }

    /// <summary>
    /// Points each asset at the image it draws, and marks the images used: an asset's
    /// <c>source</c> through the library's other names for images, or - with none - the image of its
    /// own name, or the one its name is another name for. A source with no image is dropped.
    /// </summary>
    private static void Reference(JsonObject data, ImageBundle images)
    {
        if (data["assets"] is not JsonArray assets)
            return;

        foreach (var asset in assets.OfType<JsonObject>())
        {
            if (asset["source"]?.GetValue<string>() is { } source)
            {
                source = images.Sources.GetValueOrDefault(source, source);

                if (images.Images.ContainsKey(source))
                {
                    asset["source"] = source;
                    images.Reference(source);
                }
                else
                {
                    asset.Remove("source");
                }
            }

            if (asset["name"]?.GetValue<string>() is not { } name || asset.ContainsKey("source"))
                continue;

            if (images.Images.ContainsKey(name))
                images.Reference(name);
            else if (
                images.Sources.TryGetValue(name, out var alias) && images.Images.ContainsKey(alias)
            )
            {
                asset["source"] = alias;
                images.Reference(alias);
            }
        }
    }

    /// <summary>A prebuilt library - <c>&lt;Name&gt;.json</c> beside its packed <c>&lt;Name&gt;.png</c> - or null when it is not one.</summary>
    private static NitroBundle? Prebuilt(HabLibrary hab, string? name, string? assetType)
    {
        var json = hab.Entries.FirstOrDefault(x =>
            x.Name.EndsWith(".json", StringComparison.Ordinal)
        );

        if (json is null)
            return null;

        var png = hab.Entries.FirstOrDefault(x => x.Name == json.Name[..^".json".Length] + ".png");

        if (JsonNode.Parse(hab.Get(json)) is not JsonObject legacy)
            throw new AssetFormatException($"The .hab's {json.Name} is not a JSON object.");

        var converted = LegacyAssetData.Convert(legacy);
        var bundleName = name ?? converted.DocumentClass ?? hab.Manifest.Name;
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        AssetDataFilter.Apply(converted.AssetData, assetType);
        files[$"{bundleName}.json"] = Json(converted.AssetData);

        if (png is not null)
            files[$"{bundleName}.png"] = hab.Get(png);

        if (converted.Spritesheet is { } spritesheet)
            files[$"{bundleName}_spritesheet.json"] = Json(
                LegacyAssetData.Spritesheet(
                    spritesheet,
                    converted.DocumentClass ?? bundleName,
                    $"{bundleName}.png"
                )
            );

        return NitroBundle.Create(files);
    }

    private static byte[] Json(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString(JSON));
}
