using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Turbo.Admin.Catalog;

/// <summary>
/// The colour a furni is, as a word to title a page by: a colour word in its public name (<c>Pink
/// Marquee</c>), or else the colour its furnidata part colours mostly tint it (plasto's are named
/// only <c>Chair</c>). Close names are one: aqua, aquamarine, turquoise and cyan are aqua; grey,
/// gray, silver and graphite are grey; gold and golden are gold. Azure, teal and emerald stay
/// apart, the colourable rares coming in each. Words that name things as often as colours (rose,
/// lime, mint, peach, olive) are not read, nor the cream of an ice cream.
/// </summary>
public static partial class FurniColours
{
    private static readonly Dictionary<string, string> Words = new(StringComparer.Ordinal)
    {
        ["black"] = "black",
        ["white"] = "white",
        ["grey"] = "grey",
        ["gray"] = "grey",
        ["silver"] = "grey",
        ["graphite"] = "grey",
        ["red"] = "red",
        ["maroon"] = "maroon",
        ["pink"] = "pink",
        ["magenta"] = "magenta",
        ["orange"] = "orange",
        ["yellow"] = "yellow",
        ["gold"] = "gold",
        ["golden"] = "gold",
        ["cream"] = "cream",
        ["beige"] = "beige",
        ["brown"] = "brown",
        ["bronze"] = "bronze",
        ["green"] = "green",
        ["emerald"] = "emerald",
        ["teal"] = "teal",
        ["aqua"] = "aqua",
        ["aquamarine"] = "aqua",
        ["turquoise"] = "aqua",
        ["cyan"] = "aqua",
        ["azure"] = "azure",
        ["blue"] = "blue",
        ["navy"] = "navy",
        ["purple"] = "purple",
        ["violet"] = "violet",
        ["lilac"] = "lilac",
    };

    /// <summary>The furni's colour, lower case; null when neither its name nor its part colours tell one.</summary>
    public static string? Of(string? publicName, IReadOnlyList<string>? partColors)
    {
        var name = (publicName?.ToLowerInvariant() ?? string.Empty).Replace(
            "ice cream",
            "icecream",
            StringComparison.Ordinal
        );

        foreach (Match word in WordPattern().Matches(name))
        {
            if (Words.TryGetValue(word.Value, out var colour))
                return colour;
        }

        var tints = (partColors ?? [])
            .Select(x => x.Trim().TrimStart('#').ToLowerInvariant())
            .Where(x => HexPattern().IsMatch(x))
            .ToList();

        if (tints.Count == 0)
            return null;

        // White is what most furni are tinted around; the colour is the other tint, when there is one.
        var tinted = tints.Where(x => x != "ffffff").ToList();
        var main = (tinted.Count > 0 ? tinted : tints)
            .GroupBy(x => x, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .First()
            .Key;

        return HueName(main);
    }

    /// <summary>A colour's name as a page title: <c>pink</c> is <c>Pink</c>.</summary>
    public static string Title(string colour) =>
        colour.Length == 0
            ? colour
            : char.ToUpper(colour[0], CultureInfo.InvariantCulture) + colour[1..];

    /// <summary>The plain name of an <c>rrggbb</c> colour, by its hue, how strong it is and how light.</summary>
    private static string HueName(string hex)
    {
        var r = int.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var g = int.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var b = int.Parse(hex[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var saturation = max == 0 ? 0 : (max - min) / max;

        if (max < 0.22)
            return "black";

        if (saturation < 0.15)
            return max > 0.85 ? "white" : "grey";

        var span = max - min;
        var hue =
            max == r ? 60 * ((g - b) / span % 6)
            : max == g ? 60 * ((b - r) / span + 2)
            : 60 * ((r - g) / span + 4);

        if (hue < 0)
            hue += 360;

        return hue switch
        {
            >= 15 and < 45 when max < 0.6 => "brown",
            < 15 or >= 345 => "red",
            < 45 => "orange",
            < 70 => "yellow",
            < 160 => "green",
            < 200 => "aqua",
            < 255 => "blue",
            < 290 => "purple",
            _ => "pink",
        };
    }

    [GeneratedRegex("[a-z]+")]
    private static partial Regex WordPattern();

    [GeneratedRegex("^[0-9a-f]{6}$")]
    private static partial Regex HexPattern();
}
