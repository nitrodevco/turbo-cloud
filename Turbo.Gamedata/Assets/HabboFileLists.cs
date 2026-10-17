using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Turbo.Gamedata.Texts;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata.Assets;

/// <summary>
/// The files besides the furniture, clothing, effect and pet libraries that a hotel's asset host
/// serves, as nitro-studio takes them from Habbo and names them: the room's own libraries, the
/// badges, the catalog's icons and pictures, and the trax samples. A file has no revision of
/// Habbo's: once had, it is had.
/// </summary>
internal static partial class HabboFileLists
{
    /// <summary>A file kept as it is, never older than Habbo's: the revision every one has.</summary>
    public const string STABLE = "";

    /// <summary>
    /// The room content and the objects the room engine draws itself, as the official client
    /// converts its generic libraries (<c>ConvertGenericSwfs</c>).
    /// </summary>
    public static readonly ImmutableArray<string> ROOM_LIBRARIES =
    [
        "HabboRoomContent",
        "TileCursor",
        "SelectionArrow",
        "PlaceHolderFurniture",
        "PlaceHolderWallItem",
        "PlaceHolderPet",
    ];

    /// <summary>The files numbered from <c>First</c> up, which Habbo lists nowhere: found by asking for each.</summary>
    public static readonly ImmutableArray<NumberedSet> NUMBERED =
    [
        new(AssetBundleKind.CatalogIcon, "icon_{0}.png", 1),
        new(AssetBundleKind.Sound, "sound_machine_sample_{0}.mp3", 0),
    ];

    public static List<HabboLibrary> Room(string revision) =>
        [.. ROOM_LIBRARIES.Select(x => new HabboLibrary(AssetBundleKind.Room, x, revision, null))];

    /// <summary>
    /// The badge codes Habbo's texts name (<c>badge_name_&lt;code&gt;</c>, or <c>&lt;code&gt;_badge_name</c>),
    /// as nitro-studio reads them.
    /// </summary>
    public static SortedSet<string> TextedBadges(byte[] externalTexts)
    {
        var codes = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var key in ExternalTextsFile.Parse(Encoding.UTF8.GetString(externalTexts)).Keys)
        {
            var match = BadgeTextPattern().Match(key);

            if (!match.Success)
                continue;

            var code = (match.Groups[1].Success ? match.Groups[1] : match.Groups[2]).Value.Trim();

            if (code.Length > 0)
                codes.Add(code);
        }

        return codes;
    }

    /// <summary>An achievement badge's code split into its base and level (<c>ACH_Login12</c>: <c>ACH_Login</c>, 12).</summary>
    public static (string Base, int Level)? AchievementLevel(string code)
    {
        if (
            !code.StartsWith("ACH_", StringComparison.Ordinal)
            || code.EndsWith("_HHCA", StringComparison.Ordinal)
        )
            return null;

        var match = LevelPattern().Match(code);

        return
            match.Success
            && int.TryParse(match.Groups[2].ValueSpan, CultureInfo.InvariantCulture, out var level)
            ? (match.Groups[1].Value, level)
            : null;
    }

    public static string BadgeFile(string code) => $"{code}.gif";

    public static HabboLibrary Badge(string code) =>
        new(AssetBundleKind.Badge, BadgeFile(code), STABLE, null);

    /// <summary>The headers and teasers the catalog's pages name (<c>image_data</c>), as <c>&lt;name&gt;.gif</c>.</summary>
    public static List<HabboLibrary> CatalogImages(IEnumerable<IEnumerable<string>?> pageImages) =>
        [
            .. pageImages
                .SelectMany(x => x ?? [])
                .Select(x => x.Trim())
                .Where(x => x.Length > 0 && !x.Contains('/') && !x.Contains('\\'))
                .Select(x =>
                    x.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
                    || x.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                        ? x
                        : $"{x}.gif"
                )
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(x => new HabboLibrary(AssetBundleKind.CatalogImage, x, STABLE, null)),
        ];

    /// <summary>A numbered set: its kind, its file name with <c>{0}</c> for the number, and the first number.</summary>
    public sealed record NumberedSet(AssetBundleKind Kind, string Pattern, int First)
    {
        public string FileOf(int number) =>
            string.Format(CultureInfo.InvariantCulture, Pattern, number);

        /// <summary>The number in a file of the set; null for a name of another shape.</summary>
        public int? NumberOf(string name)
        {
            var parts = Pattern.Split("{0}");

            return
                name.StartsWith(parts[0], StringComparison.Ordinal)
                && name.EndsWith(parts[1], StringComparison.Ordinal)
                && int.TryParse(
                    name.AsSpan(parts[0].Length, name.Length - parts[0].Length - parts[1].Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var number
                )
                ? number
                : null;
        }
    }

    // Unanchored, as nitro-studio has it.
    [GeneratedRegex(@"(?:badge_name_(\w+)|(\w+)_badge_name)", RegexOptions.IgnoreCase)]
    private static partial Regex BadgeTextPattern();

    [GeneratedRegex(@"^(.*?)(\d+)$")]
    private static partial Regex LevelPattern();
}
