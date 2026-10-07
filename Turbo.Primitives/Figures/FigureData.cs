using System.Collections.Generic;
using System.Linq;

namespace Turbo.Primitives.Figures;

/// <summary>
/// The hotel's figure data as figures are checked against it: each kind of clothing with its
/// pieces, and each palette with its colours. Built from the gamedata's figure records; empty
/// before any are taken in, when nothing is checked.
/// </summary>
public sealed class FigureData(
    IReadOnlyDictionary<string, FigureSetType> setTypes,
    IReadOnlyDictionary<int, FigurePalette> palettes
)
{
    public static readonly FigureData Empty = new(
        new Dictionary<string, FigureSetType>(),
        new Dictionary<int, FigurePalette>()
    );

    public IReadOnlyDictionary<string, FigureSetType> SetTypes { get; } = setTypes;

    public IReadOnlyDictionary<int, FigurePalette> Palettes { get; } = palettes;

    public bool IsEmpty => SetTypes.Count == 0;
}

/// <summary>
/// A kind of clothing (<c>hr</c>, <c>ch</c>...): the palette its pieces are coloured from, and
/// whether a figure must wear one, by gender and by whether the wearer is in the club.
/// </summary>
public sealed record FigureSetType(
    string Type,
    int PaletteId,
    bool MandatoryMale,
    bool MandatoryFemale,
    bool MandatoryMaleClub,
    bool MandatoryFemaleClub,
    IReadOnlyDictionary<int, FigureSet> Sets
)
{
    /// <summary>As the client reads it: the club column for any club level above none.</summary>
    public bool IsMandatory(char gender, int clubLevel) =>
        gender == 'F'
            ? clubLevel > 0
                ? MandatoryFemaleClub
                : MandatoryFemale
            : clubLevel > 0
                ? MandatoryMaleClub
                : MandatoryMale;
}

/// <summary>
/// A piece of clothing. A figure may wear it when it is selectable, for the wearer's gender
/// (<c>U</c> is anyone's), within their club level, and - when it is sold - owned.
/// <see cref="ColorLayers"/> is how many colours it takes.
/// </summary>
public sealed record FigureSet(
    int Id,
    char Gender,
    int Club,
    bool Colorable,
    bool Selectable,
    bool Preselectable,
    bool Sellable,
    int ColorLayers
);

public sealed record FigurePalette(int Id, IReadOnlyDictionary<int, FigureColor> Colors)
{
    /// <summary>The colour the client picks first for a wearer of this club level; null when there is none.</summary>
    public FigureColor? FirstAllowed(int clubLevel) =>
        Colors
            .Values.Where(x => x.Selectable && x.Club <= clubLevel)
            .OrderBy(x => x.Club)
            .ThenBy(x => x.Index)
            .ThenBy(x => x.Id)
            .FirstOrDefault();
}

public sealed record FigureColor(int Id, int Index, int Club, bool Selectable);
