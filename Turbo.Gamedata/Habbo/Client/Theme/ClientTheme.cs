using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Turbo.Assets;
using Turbo.Assets.Conversion;
using Turbo.Assets.Hab;

namespace Turbo.Gamedata.Habbo.Client.Theme;

/// <summary>
/// What the UI theme adds to the window manager library's bundle: <see cref="ClientTheme.SHEET"/>,
/// <see cref="ClientTheme.SPRITESHEET"/> and <see cref="ClientTheme.VARIANTS"/> by file name, and the
/// sprites' asset names (<c>theme-...</c>), sorted, which go into the bundle's
/// <c>habbo-window-manager-com.json</c> <c>assets</c> after the library's own.
/// </summary>
internal sealed record ClientThemeFiles(
    IReadOnlyDictionary<string, byte[]> Files,
    IReadOnlyList<string> AssetNames
);

/// <summary>
/// The UI theme nitro-react draws its chrome with, built from a release of Habbo's HTML client the
/// way Nitro Studio builds it (<c>server/theme/clientTheme.ts</c>, and <c>composeThemeBundle</c> /
/// <c>withTheme</c> in <c>server/ui-theme.ts</c>), from the client's window manager library alone:
/// Studio's workspace edits and own sprites have no counterpart here.
/// <para>
/// Every variant the port's rules name is built from the library's element description, window
/// layouts and skins (<see cref="ThemeVariants"/>, <see cref="SkinArt"/>), with the icon set; a
/// bitmap cut in several places is kept once, under the first name in order, and every texture key
/// naming a copy renamed to it. The sprites are packed into <c>theme.png</c> with each edge extruded
/// into a gutter (<see cref="ThemeSheetPacker"/>), <c>theme_spritesheet.json</c> names their rects,
/// and <c>theme-variants.json</c> holds the variants, what each cascades to its children and the
/// icon set's rects. nitro-react's <c>preloadThemeAssets</c> reads them out of the bundle, a sprite
/// <c>theme-&lt;x&gt;</c> being the texture key <c>&lt;x&gt;-src</c>.
/// </para>
/// </summary>
internal static class ClientTheme
{
    public const string SHEET = "theme.png";
    public const string SPRITESHEET = "theme_spritesheet.json";
    public const string VARIANTS = "theme-variants.json";

    /// <summary>What the sheet and its manifest are named after.</summary>
    private const string SHEET_NAME = "theme";

    private static readonly JsonSerializerOptions JSON = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// The theme a window manager library draws; null when it yields no sprite. Throws a
    /// <see cref="FormatException"/> for a library it cannot read (no element description, a bitmap
    /// that is not an image).
    /// </summary>
    public static ClientThemeFiles? Build(HabLibrary windowManager)
    {
        try
        {
            var library = new ThemeLibrary(windowManager);
            var (file, sprites) = ThemeVariants.Build(library, ThemeRules.Read());

            if (ThemeVariants.IconSet(library) is { } iconSet)
            {
                sprites[ThemeVariants.ICON_SET_ASSET] = iconSet.Image;
                file["icons"] = iconSet.Icons;
            }

            var (variants, unique) = Dedupe(file, sprites);

            if (unique.Count == 0)
                return null;

            // Studio's build lays the workspace's texture keys over the client's; there are none here.
            variants["textures"] = new JsonObject();

            var names = unique.Keys.Order(StringComparer.Ordinal).ToList();
            var (sheet, spritesheet) = ThemeSheetPacker.Pack(SHEET_NAME, unique);

            return new ClientThemeFiles(
                new Dictionary<string, byte[]>
                {
                    [SHEET] = sheet.EncodePng(),
                    [SPRITESHEET] = Encode(spritesheet),
                    [VARIANTS] = Encode(variants),
                },
                names
            );
        }
        catch (AssetFormatException ex)
        {
            throw new FormatException(
                $"The window manager library's theme could not be built: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// One sprite per bitmap: a piece every state shares, or a corner two groups cut alike, is cut
    /// once per place that names it; it is kept once, under the first name in order, and every field
    /// naming a copy's texture key renamed to the kept one's.
    /// </summary>
    private static (JsonObject File, Dictionary<string, RgbaImage> Sprites) Dedupe(
        JsonObject file,
        Dictionary<string, RgbaImage> sprites
    )
    {
        var kept = new Dictionary<string, List<(string Name, RgbaImage Image)>>();
        var renamed = new Dictionary<string, string>();
        var unique = new Dictionary<string, RgbaImage>();

        foreach (var name in sprites.Keys.Order(StringComparer.Ordinal))
        {
            var image = sprites[name];
            var hash =
                Convert.ToHexString(SHA1.HashData(image.Pixels)) + $"{image.Width}x{image.Height}";
            var candidates = kept.TryGetValue(hash, out var list) ? list : kept[hash] = [];
            var first = candidates.FirstOrDefault(x => x.Image.SameAs(image));

            if (first.Name is not null)
            {
                renamed[SkinArt.TextureKeyOf(name)] = SkinArt.TextureKeyOf(first.Name);

                continue;
            }

            candidates.Add((name, image));
            unique[name] = image;
        }

        JsonNode? Rename(JsonNode? value)
        {
            switch (value)
            {
                case JsonArray array:
                    return new JsonArray([.. array.Select(Rename)]);
                case JsonObject obj:
                {
                    var result = new JsonObject();

                    foreach (var (field, entry) in obj)
                    {
                        result[field] =
                            entry is JsonValue leaf
                            && leaf.TryGetValue<string>(out var text)
                            && renamed.TryGetValue(text, out var to)
                                ? to
                                : Rename(entry);
                    }

                    return result;
                }
                default:
                    // A string in an array is left as it is, as Studio's rename leaves it.
                    return value?.DeepClone();
            }
        }

        return (Rename(file)!.AsObject(), unique);
    }

    private static byte[] Encode(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString(JSON));
}
