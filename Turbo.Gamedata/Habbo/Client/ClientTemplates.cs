using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Turbo.Assets.Conversion;
using Turbo.Assets.Hab;

namespace Turbo.Gamedata.Habbo.Client;

/// <summary>
/// The window template bundles, packed as nitro-studio packs them
/// (<c>server/templates/packTemplateBundles.ts</c>) from a client release's libraries: one bundle per
/// library, named after it - <c>templates.json</c> (every <c>&lt;layout&gt;</c> converted and keyed
/// <c>&lt;library&gt;/&lt;asset&gt;</c>), the asset data naming its bitmaps, and each bitmap as
/// <c>&lt;name&gt;.png</c>.
/// <para>
/// A bundle carries every bitmap its library has, the ones no layout names too, since the client's
/// views draw a library's art by name (nitro-react's <c>LayoutImage('&lt;library&gt;/&lt;asset&gt;.png')</c>).
/// A bitmap is named <c>&lt;library&gt;-&lt;asset&gt;</c> after the library it is found in - the
/// layout's own, else the window manager's, which every library draws from - and a window manager
/// bitmap goes into the window manager's bundle only, so no two bundles carry one name.
/// </para>
/// </summary>
internal static partial class ClientTemplates
{
    public const string WINDOW_MANAGER = "habbo-window-manager-com";
    public const string TEMPLATES_FILE = "templates.json";

    /// <summary>The window types the layout model builds from a window layout of the element description: a template carries the ones its elements use.</summary>
    private static readonly HashSet<string> SKINNED_TYPES = new(StringComparer.Ordinal)
    {
        "scrollable_itemlist_vertical",
        "scrollable_itemgrid_vertical",
        "tab_context",
    };

    private static readonly JsonSerializerOptions JSON = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Each library's bundle files, and what could not be packed (<c>&lt;library&gt;: &lt;asset&gt;</c>, <c>&lt;library&gt;: &lt;layout&gt; (&lt;why&gt;)</c>).</summary>
    public sealed record Packed(
        Dictionary<string, Dictionary<string, byte[]>> Bundles,
        List<string> Problems
    );

    /// <summary>Whether a library holds a window template (a <c>&lt;layout&gt;</c> XML).</summary>
    public static bool HasLayouts(HabLibrary hab) => Layouts(hab).Any();

    /// <summary>
    /// Every library's templates and bitmaps packed into its bundle; <paramref name="windowManager"/>
    /// is the library every other one falls back to, among <paramref name="libraries"/> or not.
    /// </summary>
    public static Packed Pack(
        string version,
        IReadOnlyList<(string Name, HabLibrary Hab)> libraries,
        HabLibrary windowManager
    )
    {
        var skinLayout = SkinLayouts(windowManager);
        var skins = new Dictionary<string, JsonObject?>(StringComparer.Ordinal);
        var bitmapsOf = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal);
        var templatesOf = new List<(string Library, JsonObject Templates)>();
        var problems = new SortedSet<string>(StringComparer.Ordinal);
        var all = libraries.Any(x => x.Name == WINDOW_MANAGER)
            ? libraries
            : [.. libraries, (WINDOW_MANAGER, windowManager)];

        JsonObject? SkinOf(string type, string style)
        {
            var key = SkinKey(type, style);

            if (!skins.TryGetValue(key, out var skin))
                skins[key] = skin = skinLayout(type, style) is { } xml
                    ? LayoutTemplateMapper.Map(xml)
                    : null;

            return skin;
        }

        void SkinsFor(JsonArray? elements, JsonObject found)
        {
            foreach (var element in elements?.OfType<JsonObject>() ?? [])
            {
                var tag = element["tag"]!.GetValue<string>();
                var style = element["style"]?.GetValue<string>();

                if (SKINNED_TYPES.Contains(tag) && SkinOf(tag, style ?? "0") is { } skin)
                    found[SkinKey(tag, style)] = skin.DeepClone();

                SkinsFor(element["children"] as JsonArray, found);
            }
        }

        Dictionary<string, byte[]> Bitmaps(string library)
        {
            if (!bitmapsOf.TryGetValue(library, out var found))
                bitmapsOf[library] = found = new(StringComparer.Ordinal);

            return found;
        }

        foreach (var (name, hab) in all)
        {
            var templates = new JsonObject();
            var windowManagerOnly =
                name == WINDOW_MANAGER && !libraries.Any(x => x.Name == WINDOW_MANAGER);

            string? ResolveAsset(string asset)
            {
                // A url (${image.library.url}...) or a shared variable is the client's to fill in.
                if (asset.Contains('/') || asset.Contains('$'))
                    return null;

                foreach (
                    var (source, sourceHab) in new[]
                    {
                        (name, hab),
                        (WINDOW_MANAGER, windowManager),
                    }
                )
                {
                    var assetName = AssetName(source, asset);
                    var held = Bitmaps(source);

                    if (held.ContainsKey(assetName))
                        return assetName;

                    if (Bitmap(sourceHab, asset) is { } data)
                    {
                        held[assetName] = data;

                        return assetName;
                    }
                }

                problems.Add($"{name}: {asset}");

                return null;
            }

            foreach (
                var (entry, xml) in windowManagerOnly
                    ? Enumerable.Empty<(string Entry, string Xml)>()
                    : Layouts(hab).OrderBy(x => x.Entry, StringComparer.Ordinal)
            )
            {
                try
                {
                    var template = LayoutTemplateMapper.Map(
                        xml,
                        ResolveAsset,
                        frame =>
                            LayoutTemplateMapper.FrameMargins(
                                frame,
                                LayoutTemplateMapper.ContentMargins(
                                    SkinOf(
                                        frame["tag"]!.GetValue<string>(),
                                        frame["style"]?.GetValue<string>() ?? "0"
                                    )
                                )
                            )
                    );

                    if (template is null)
                        continue;

                    var used = new JsonObject();

                    SkinsFor(template["elements"] as JsonArray, used);

                    if (used.Count > 0)
                        template["skins"] = used;

                    templates[$"{name}/{entry}"] = template;
                }
                catch (FormatException ex)
                {
                    // A layout the client itself would refuse is left out, not the library.
                    problems.Add($"{name}: {entry} ({ex.Message})");
                }
            }

            templatesOf.Add((name, templates));
        }

        // Every bitmap a library has goes into its bundle, named by a layout or not.
        foreach (var (name, hab) in all)
        {
            var held = Bitmaps(name);

            foreach (var asset in BitmapNames(hab))
            {
                var assetName = AssetName(name, asset);

                if (!held.ContainsKey(assetName) && Bitmap(hab, asset) is { } data)
                    held[assetName] = data;
            }
        }

        var bundles = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal);

        foreach (var (library, templates) in templatesOf)
        {
            var held = bitmapsOf.GetValueOrDefault(library) ?? [];

            if (templates.Count == 0 && held.Count == 0)
                continue;

            var names = held.Keys.Order(StringComparer.Ordinal).ToList();
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [$"{library}.json"] = Json(
                    new JsonObject
                    {
                        ["type"] = library,
                        ["assets"] = new JsonArray([
                            .. names.Select(x => (JsonNode)new JsonObject { ["name"] = x }),
                        ]),
                    }
                ),
                [TEMPLATES_FILE] = Json(
                    new JsonObject
                    {
                        ["library"] = library,
                        ["version"] = version,
                        ["templates"] = templates,
                    }
                ),
            };

            foreach (var asset in names)
                files[$"{asset}.png"] = held[asset];

            bundles[library] = files;
        }

        return new Packed(bundles, [.. problems]);
    }

    /// <summary>A skin's key: the window type and style it lays out (<c>scrollable_itemlist_vertical:0</c>).</summary>
    private static string SkinKey(string type, string? style) => $"{type}:{style ?? "0"}";

    /// <summary>
    /// The window layouts of the window manager's element description
    /// (<c>habbo_element_description_xml</c>): a window type and style's row, else style 0's.
    /// </summary>
    private static Func<string, string, string?> SkinLayouts(HabLibrary windowManager)
    {
        var rows = new List<(string Type, string Style, string? WindowLayout)>();

        if (
            Text(windowManager, "habbo_element_description_xml") is { } description
            && LayoutTemplateMapper.Parse(description) is { } root
        )
        {
            void Walk(LayoutTemplateMapper.Node node)
            {
                if (
                    node.Tag == "window"
                    && node.Attrs.GetValueOrDefault("type") is { Length: > 0 } type
                    && node.Attrs.TryGetValue("style", out var style)
                )
                    rows.Add(
                        (
                            type,
                            style,
                            node.Attrs.GetValueOrDefault("window_layout") is { } layout
                                ? XmlSuffixPattern().Replace(layout, "")
                                : null
                        )
                    );

                foreach (var child in node.Children)
                    Walk(child);
            }

            Walk(root);
        }

        return (type, style) =>
        {
            var row = rows.FirstOrDefault(x =>
                x.Type == type && x.Style == style && !string.IsNullOrEmpty(x.WindowLayout)
            );

            if (row.Type is null)
                row = rows.FirstOrDefault(x =>
                    x.Type == type && x.Style == "0" && !string.IsNullOrEmpty(x.WindowLayout)
                );

            return row.WindowLayout is { Length: > 0 } layout
                ? Text(windowManager, $"{layout}_xml")
                : null;
        };
    }

    /// <summary>A library's <c>&lt;layout&gt;</c> XMLs, by asset name (<c>purse_xml</c>).</summary>
    private static IEnumerable<(string Entry, string Xml)> Layouts(HabLibrary hab)
    {
        foreach (var entry in hab.Entries)
        {
            if (!entry.MimeType.Contains("xml", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = Encoding.UTF8.GetString(hab.Get(entry));

            if (LayoutTemplateMapper.RootTag(text) == "layout")
                yield return (entry.Name, text);
        }
    }

    /// <summary>
    /// A bitmap of a library by asset name, found as the client finds it: an image of its own (with or
    /// without its <c>_png</c>), or an alias of one cut to its <c>region</c>; null when it has none.
    /// </summary>
    private static byte[]? Bitmap(HabLibrary hab, string asset)
    {
        foreach (var name in new[] { asset, $"{asset}_png" })
        {
            if (hab.Entries.FirstOrDefault(x => x.Name == name) is { } entry)
                return entry.MimeType.StartsWith("image/", StringComparison.Ordinal)
                    ? hab.Get(entry)
                    : null;

            var alias = hab.Manifest.Aliases?.FirstOrDefault(x => x.Name == name);

            if (alias is not null && hab.GetBinary(alias.Ref) is { } data)
                return alias.Params?.GetValueOrDefault("region") is { } region
                    ? Crop(data, region)
                    : data;
        }

        return null;
    }

    /// <summary>Every bitmap a library has, by the name <see cref="Bitmap"/> finds it by: its images, and the aliases naming one.</summary>
    private static IEnumerable<string> BitmapNames(HabLibrary hab)
    {
        var images = hab
            .Entries.Where(x => x.MimeType.StartsWith("image/", StringComparison.Ordinal))
            .Select(x => x.Name)
            .ToList();
        var known = images.ToHashSet(StringComparer.Ordinal);
        var aliases = (hab.Manifest.Aliases ?? [])
            .Where(x =>
                x.MimeType.StartsWith("image/", StringComparison.Ordinal) || known.Contains(x.Ref)
            )
            .Select(x => x.Name);

        return images.Concat(aliases).Select(BitmapName);
    }

    /// <summary>A region of a PNG, as an alias's <c>region</c> (<c>x,y,width,height</c>) cuts it; the whole bitmap for a region that isn't one.</summary>
    private static byte[] Crop(byte[] data, string region)
    {
        var parts = region
            .Split(',')
            .Select(x => int.TryParse(x.Trim(), out var v) ? v : (int?)null)
            .ToList();

        if (parts.Count < 4 || parts.Any(x => x is null) || parts[2] <= 0 || parts[3] <= 0)
            return data;

        var (x, y, width, height) = (
            parts[0]!.Value,
            parts[1]!.Value,
            parts[2]!.Value,
            parts[3]!.Value
        );
        var source = RgbaImage.Decode(data);
        var copyWidth = Math.Min(width, source.Width - x);
        var copyHeight = Math.Min(height, source.Height - y);

        if (x < 0 || y < 0 || copyWidth <= 0 || copyHeight <= 0)
            return new RgbaImage(width, height, new byte[width * height * 4]).EncodePng();

        return source.Crop(x, y, copyWidth, copyHeight, width, height).EncodePng();
    }

    /// <summary>An image asset's name without the type its library may file it under (<c>roomtools_gear_png</c> is <c>roomtools_gear</c>).</summary>
    private static string BitmapName(string asset) => BitmapSuffixPattern().Replace(asset, "");

    /// <summary>
    /// A bundle's asset name for a library's bitmap, as <c>LayoutImage('&lt;library&gt;/&lt;asset&gt;.png')</c>
    /// builds it: <c>habbo-room-ui-com-roomtools_gear</c>.
    /// </summary>
    internal static string AssetName(string library, string asset) =>
        NotWordPattern().Replace($"{library}-{BitmapName(asset)}", "-");

    private static string? Text(HabLibrary hab, string name) =>
        hab.Entries.FirstOrDefault(x => x.Name == name) is { } entry
            ? Encoding.UTF8.GetString(hab.Get(entry))
            : null;

    private static byte[] Json(JsonNode node) => JsonSerializer.SerializeToUtf8Bytes(node, JSON);

    [GeneratedRegex("_(?:png|gif|jpe?g)$", RegexOptions.IgnoreCase)]
    private static partial Regex BitmapSuffixPattern();

    // JavaScript's \w: ASCII only.
    [GeneratedRegex("[^A-Za-z0-9_-]")]
    private static partial Regex NotWordPattern();

    [GeneratedRegex("_xml$")]
    private static partial Regex XmlSuffixPattern();
}
