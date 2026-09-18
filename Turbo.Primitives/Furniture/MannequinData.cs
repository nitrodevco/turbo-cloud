using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// The map keys the client's mannequin logic reads, and the figure parts a mannequin holds:
/// only clothing, never head or body.
/// </summary>
public static class MannequinData
{
    public const string GENDER = "GENDER";
    public const string FIGURE = "FIGURE";
    public const string OUTFIT_NAME = "OUTFIT_NAME";

    private const char PART_SEPARATOR = '.';
    private const char FIELD_SEPARATOR = '-';

    public static readonly ImmutableHashSet<string> ClothingParts =
    [
        "ch",
        "cc",
        "cp",
        "lg",
        "sh",
        "wa",
    ];

    /// <summary>The clothing parts of a full figure string.</summary>
    public static string ExtractClothing(string figure) =>
        string.Join(PART_SEPARATOR, Parts(figure).Where(IsClothing));

    /// <summary>The wearer's figure with its clothing replaced by the mannequin's.</summary>
    public static string Dress(string wearerFigure, string mannequinFigure) =>
        string.Join(
            PART_SEPARATOR,
            Parts(wearerFigure).Where(part => !IsClothing(part)).Concat(Parts(mannequinFigure))
        );

    private static IEnumerable<string> Parts(string figure) =>
        figure.Split(PART_SEPARATOR, StringSplitOptions.RemoveEmptyEntries);

    private static bool IsClothing(string part)
    {
        var separator = part.IndexOf(FIELD_SEPARATOR);

        return separator > 0 && ClothingParts.Contains(part[..separator]);
    }
}
