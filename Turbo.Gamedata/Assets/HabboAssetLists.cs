using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Turbo.Gamedata.Habbo;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata.Assets;

/// <summary>
/// The libraries Habbo serves, as a sync reads them from its files: furniture from furnidata,
/// clothing from <c>figuremap.xml</c>, effects from <c>effectmap.xml</c>, pets from
/// <c>pet.configuration</c>.
/// </summary>
internal static class HabboAssetLists
{
    /// <summary>The external variable that lists the pet types, comma separated, in type order.</summary>
    public const string PET_CONFIGURATION_VARIABLE = "pet.configuration";

    /// <summary>The effect map's name among Habbo's client files.</summary>
    public const string EFFECT_MAP = "effectmap.xml";

    /// <summary>The figure (clothing) map's name among Habbo's client files.</summary>
    public const string FIGURE_MAP = "figuremap.xml";

    /// <summary>
    /// Libraries the figure map names that are not clothing bundles: pets and avatar effects are
    /// drawn from their own, as nitro-studio leaves them out too.
    /// </summary>
    public static readonly IReadOnlySet<string> FIGURE_LIBRARIES_SKIPPED = new HashSet<string>(
        StringComparer.Ordinal
    )
    {
        "hh_pets",
        "hh_human_fx",
    };

    private static readonly string[] FURNITURE_LISTS = ["roomitemtypes", "wallitemtypes"];

    /// <summary>
    /// Each furniture asset furnidata names (its classname without the <c>*N</c> colour), at the
    /// highest revision any of its items has.
    /// </summary>
    public static List<HabboLibrary> Furniture(byte[] furnidata)
    {
        using var document = JsonDocument.Parse(furnidata);
        var revisions = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var list in FURNITURE_LISTS)
        {
            if (
                !document.RootElement.TryGetProperty(list, out var types)
                || !types.TryGetProperty("furnitype", out var items)
                || items.ValueKind != JsonValueKind.Array
            )
                continue;

            foreach (var item in items.EnumerateArray())
            {
                if (
                    !item.TryGetProperty("classname", out var className)
                    || className.ValueKind != JsonValueKind.String
                )
                    continue;

                var name = HabboFurnitureFiles.AssetName(className.GetString()!);

                if (name.Length == 0)
                    continue;

                var revision = item.TryGetProperty("revision", out var value) ? IntOf(value) : 0;

                revisions[name] = Math.Max(revisions.GetValueOrDefault(name), revision);
            }
        }

        return
        [
            .. revisions
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new HabboLibrary(
                    AssetBundleKind.Furniture,
                    x.Key,
                    x.Value.ToString(CultureInfo.InvariantCulture),
                    null
                )),
        ];
    }

    /// <summary>
    /// Each library the effect map names (<c>&lt;effect id lib type revision&gt;</c>), with the
    /// effects that use it and the highest revision among them. Throws
    /// <see cref="HttpRequestException"/> for a file that is not an effect map, as Habbo's filter
    /// answers with a page of HTML.
    /// </summary>
    public static List<HabboLibrary> Effects(byte[] effectMap) =>
        [
            .. Map(effectMap, "effect map")
                .Descendants("effect")
                .Select(x =>
                    (
                        Lib: ((string?)x.Attribute("lib"))?.Trim() ?? string.Empty,
                        Id: IntOf((string?)x.Attribute("id")),
                        Revision: IntOf((string?)x.Attribute("revision"))
                    )
                )
                .Where(x => x.Lib.Length > 0)
                .GroupBy(x => x.Lib, StringComparer.Ordinal)
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new HabboLibrary(
                    AssetBundleKind.Effect,
                    x.Key,
                    x.Max(y => y.Revision).ToString(CultureInfo.InvariantCulture),
                    AssetBundleIds.Format(x.Select(y => y.Id).Where(y => y > 0))
                )),
        ];

    /// <summary>
    /// Each clothing library the figure map names (<c>&lt;lib id revision&gt;</c>), but those drawn
    /// from elsewhere (<see cref="FIGURE_LIBRARIES_SKIPPED"/>), at its highest revision. Throws
    /// <see cref="HttpRequestException"/> for a file that is not a figure map.
    /// </summary>
    public static List<HabboLibrary> Figures(byte[] figureMap) =>
        [
            .. Map(figureMap, "figure map")
                .Descendants("lib")
                .Select(x =>
                    (
                        Lib: ((string?)x.Attribute("id"))?.Trim() ?? string.Empty,
                        Revision: IntOf((string?)x.Attribute("revision"))
                    )
                )
                .Where(x => x.Lib.Length > 0 && !FIGURE_LIBRARIES_SKIPPED.Contains(x.Lib))
                .GroupBy(x => x.Lib, StringComparer.Ordinal)
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new HabboLibrary(
                    AssetBundleKind.Figure,
                    x.Key,
                    x.Max(y => y.Revision).ToString(CultureInfo.InvariantCulture),
                    null
                )),
        ];

    /// <summary>
    /// A map's <c>&lt;map&gt;</c> root. Throws <see cref="HttpRequestException"/> when the file is not
    /// one, as Habbo's filter answers with a page of HTML.
    /// </summary>
    private static XElement Map(byte[] file, string what)
    {
        XDocument document;

        try
        {
            using var reader = XmlReader.Create(
                new MemoryStream(file),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }
            );

            document = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new HttpRequestException(
                $"Habbo's {what} is not XML; its filter may have refused the request.",
                ex
            );
        }

        if (document.Root is not { Name.LocalName: "map" } root)
            throw new HttpRequestException(
                $"Habbo's {what} is not a map; its filter may have refused the request."
            );

        return root;
    }

    /// <summary>
    /// Each pet <c>pet.configuration</c> names, its type being its place in the list (as the client
    /// counts them, empty places included), at the client's revision.
    /// </summary>
    public static List<HabboLibrary> Pets(string? configuration, string revision)
    {
        var pets = new List<HabboLibrary>();

        if (string.IsNullOrWhiteSpace(configuration))
            return pets;

        var parts = configuration.Split(',');

        for (var type = 0; type < parts.Length; type++)
        {
            var name = parts[type].Trim();

            if (name.Length == 0 || pets.Any(x => x.Name == name))
                continue;

            pets.Add(
                new HabboLibrary(AssetBundleKind.Pet, name, revision, AssetBundleIds.Format([type]))
            );
        }

        return pets;
    }

    private static int IntOf(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String => IntOf(value.GetString()),
            _ => 0,
        };

    private static int IntOf(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : 0;
}
