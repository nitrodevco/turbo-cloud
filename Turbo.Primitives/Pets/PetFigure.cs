using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Primitives.Pets;

/// <summary>
/// The pet figure string the client's pet renderer parses:
/// <c>&lt;typeId&gt; &lt;paletteId&gt; &lt;colorHex&gt; &lt;customPartCount&gt; (&lt;layer&gt; &lt;part&gt; &lt;palette&gt;)*</c>.
/// The wire struct sent inside pet packets carries the same fields plus the breed id.
/// </summary>
public static class PetFigure
{
    public const string DEFAULT_COLOR = "FFFFFF";
    public const char SEPARATOR = ' ';
    public const int COLOR_LENGTH = 6;

    /// <summary>Ints per custom part: layer id, part id, palette id.</summary>
    public const int CUSTOM_PART_FIELDS = 3;

    public static bool IsValidColor(string? color)
    {
        if (color is null)
            return false;

        color = color.Trim();

        if (color.Length != COLOR_LENGTH)
            return false;

        foreach (var c in color)
        {
            if (!Uri.IsHexDigit(c))
                return false;
        }

        return true;
    }

    public static string ToFigureString(PetFigureSnapshot figure)
    {
        var builder = new StringBuilder();

        builder
            .Append(figure.TypeId.ToString(CultureInfo.InvariantCulture))
            .Append(SEPARATOR)
            .Append(figure.PaletteId.ToString(CultureInfo.InvariantCulture))
            .Append(SEPARATOR)
            .Append(figure.Color)
            .Append(SEPARATOR)
            .Append(
                (figure.CustomParts.Length / CUSTOM_PART_FIELDS).ToString(
                    CultureInfo.InvariantCulture
                )
            );

        foreach (var value in figure.CustomParts)
            builder.Append(SEPARATOR).Append(value.ToString(CultureInfo.InvariantCulture));

        return builder.ToString();
    }

    /// <summary>Custom parts as stored: the flat ints joined by spaces, empty for none.</summary>
    public static string SerializeCustomParts(ImmutableArray<int> customParts) =>
        string.Join(SEPARATOR, customParts);

    public static ImmutableArray<int> ParseCustomParts(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return [];

        var values = new List<int>();

        foreach (var token in stored.Split(SEPARATOR, StringSplitOptions.RemoveEmptyEntries))
        {
            if (
                int.TryParse(
                    token,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var value
                )
            )
                values.Add(value);
        }

        // A dangling partial triple is meaningless to the client; drop it.
        var usable = values.Count - values.Count % CUSTOM_PART_FIELDS;

        return [.. values.GetRange(0, usable)];
    }
}
