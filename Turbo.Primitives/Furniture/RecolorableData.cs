using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// A recolorable furni (the Builders Club building blocks, <c>*_recolorable</c>): map data with
/// the channels <c>r</c>, <c>g</c> and <c>b</c>, each <c>0..255</c>, which the client's
/// recolorable logic reads into one colour (white when a channel is missing or out of range),
/// and which colours a furni may take: its palette, from the same two settings the client reads
/// (<c>recolorable_bc.palette_to_furni</c> = <c>palette=class1,class2;...</c> and
/// <c>recolorable_bc.palette.&lt;name&gt;</c> = <c>RRGGBB,...</c>, the palette <c>default</c>
/// for a class no entry names).
/// </summary>
public static partial class RecolorableData
{
    public const string LOGIC_NAME = "recolorable";

    /// <summary>Habbo names every recolorable class <c>*_recolorable</c>.</summary>
    private const string CLASS_SUFFIX = "_recolorable";

    public const string RED = "r";
    public const string GREEN = "g";
    public const string BLUE = "b";
    public const string STATE = "state";

    /// <summary>The colour of a furni whose channels are missing.</summary>
    public const int WHITE = 0xFFFFFF;

    /// <summary>The palette of every class <c>palette_to_furni</c> does not name.</summary>
    public const string DEFAULT_PALETTE = "default";

    private const int MAX_CHANNEL = 255;

    /// <summary>The colour the map data holds, read as the client reads it.</summary>
    public static int ReadColor(IReadOnlyDictionary<string, string> data) =>
        (Channel(data, RED) << 16) | (Channel(data, GREEN) << 8) | Channel(data, BLUE);

    /// <summary>The map entries that give a furni this colour.</summary>
    public static Dictionary<string, string> Entries(int rgb) =>
        new()
        {
            [RED] = Red(rgb).ToString(CultureInfo.InvariantCulture),
            [GREEN] = Green(rgb).ToString(CultureInfo.InvariantCulture),
            [BLUE] = Blue(rgb).ToString(CultureInfo.InvariantCulture),
        };

    public static int Red(int rgb) => (rgb >> 16) & 0xFF;

    public static int Green(int rgb) => (rgb >> 8) & 0xFF;

    public static int Blue(int rgb) => rgb & 0xFF;

    /// <summary>A colour from its channels; false when one is outside <c>0..255</c>.</summary>
    public static bool TryCombine(int red, int green, int blue, out int rgb)
    {
        rgb = (red << 16) | (green << 8) | blue;

        return IsChannel(red) && IsChannel(green) && IsChannel(blue);
    }

    /// <summary>
    /// The catalogue's purchase parameter, <c>r,g,b</c> (the recolorable catalogue widget sets
    /// it on every offer of its page, and a Builders Club placement sends it).
    /// </summary>
    public static bool TryParseParam(string? extraParam, out int rgb)
    {
        rgb = WHITE;

        var parts = (extraParam ?? string.Empty).Split(',');

        return parts.Length == 3
            && TryParseChannel(parts[0], out var red)
            && TryParseChannel(parts[1], out var green)
            && TryParseChannel(parts[2], out var blue)
            && TryCombine(red, green, blue, out rgb);
    }

    /// <summary>
    /// The colours a furni class may take, in palette order: Flash <c>getColors</c> over the two
    /// settings. Empty when its palette is not set; a colour that is not six hex digits is left
    /// out.
    /// </summary>
    public static IReadOnlyList<int> GetPalette(
        string? paletteToFurni,
        IReadOnlyDictionary<string, string>? palettes,
        string className
    )
    {
        var paletteName = DEFAULT_PALETTE;

        foreach (var entry in (paletteToFurni ?? string.Empty).Split(';'))
        {
            var pair = entry.Split('=');

            if (pair.Length != 2)
                continue;

            if (pair[1].Split(',').Any(x => x.Trim() == className))
                paletteName = pair[0].Trim();
        }

        if (palettes is null || !palettes.TryGetValue(paletteName, out var colors))
            return [];

        return
        [
            .. colors
                .Split(',')
                .Select(x => x.Trim())
                .Where(x => HexColor().IsMatch(x))
                .Select(x => int.Parse(x, NumberStyles.HexNumber, CultureInfo.InvariantCulture)),
        ];
    }

    /// <summary>
    /// The class a definition's name stands for: Habbo names colour variants <c>name*N</c>, and
    /// the client looks palettes up by the plain class name.
    /// </summary>
    public static string ClassName(string definitionName)
    {
        var star = definitionName.IndexOf('*', StringComparison.Ordinal);

        return star < 0 ? definitionName : definitionName[..star];
    }

    /// <summary>Flash <c>readChannel</c>: up to three digits and at most 255, else 255.</summary>
    /// <summary>Whether a definition's class is one of Habbo's recolorable furni.</summary>
    public static bool IsRecolorableClass(string definitionName) =>
        ClassName(definitionName).EndsWith(CLASS_SUFFIX, StringComparison.Ordinal);

    private static int Channel(IReadOnlyDictionary<string, string> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || !ChannelText().IsMatch(value))
            return MAX_CHANNEL;

        var channel = int.Parse(value, CultureInfo.InvariantCulture);

        return channel <= MAX_CHANNEL ? channel : MAX_CHANNEL;
    }

    private static bool TryParseChannel(string text, out int channel) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out channel)
        && IsChannel(channel);

    private static bool IsChannel(int value) => value is >= 0 and <= MAX_CHANNEL;

    [GeneratedRegex("^[0-9]{1,3}$")]
    private static partial Regex ChannelText();

    [GeneratedRegex("^[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
