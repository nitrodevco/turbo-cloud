using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Figures;

/// <summary>
/// What a player may wear, by the figure data and the rules the client's avatar editor offers
/// clothing by: a piece must be selectable, for the wearer's gender, within their club level and,
/// when it is sold, owned; a colour selectable and within their club level; each kind of clothing
/// worn at most once, and those the figure data makes mandatory always.
/// <para>
/// A figure is made fit to wear rather than refused, so a look saved before a rule held - club
/// clothing after the club ran out - loses only what may no longer be worn: a piece is taken off
/// (or, when its kind is mandatory, swapped for the first the wearer may have), a colour swapped
/// for the first the wearer may use, and anything the figure data doesn't know left out.
/// </para>
/// </summary>
public static class FigureRules
{
    public const char MALE = 'M';
    public const char FEMALE = 'F';
    public const char UNISEX = 'U';

    /// <summary>The club level a club member wears at, as the client is told (VIP).</summary>
    public const int CLUB_LEVEL = 2;

    public static char GenderOf(AvatarGenderType gender) =>
        gender == AvatarGenderType.Female ? FEMALE : MALE;

    /// <summary>
    /// The figure as the wearer may wear it: the same when they may wear all of it. With no
    /// figure data, the figure as given.
    /// </summary>
    public static string Fit(
        FigureData data,
        string figure,
        AvatarGenderType gender,
        int clubLevel,
        IReadOnlySet<int> owned
    )
    {
        if (data.IsEmpty)
            return figure;

        var wearer = GenderOf(gender);
        var worn = new List<(FigureSetType Type, FigureSet Set, List<int> Colors)>();

        foreach (var part in figure.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = part.Split('-');

            if (
                fields.Length < 2
                || !data.SetTypes.TryGetValue(fields[0], out var type)
                || worn.Any(x => x.Type == type)
                || !TryNumber(fields[1], out var setId)
                || !type.Sets.TryGetValue(setId, out var set)
                || !MayWear(set, wearer, clubLevel, owned)
            )
                continue;

            var colors = new List<int>();

            foreach (var field in fields.Skip(2))
                if (TryNumber(field, out var color))
                    colors.Add(color);

            worn.Add((type, set, Colors(data, type, set, colors, clubLevel)));
        }

        // A mandatory kind taken off, or never worn, gets the first piece the wearer may have.
        foreach (var type in data.SetTypes.Values)
        {
            if (!type.IsMandatory(wearer, clubLevel) || worn.Any(x => x.Type == type))
                continue;

            var set = type
                .Sets.Values.Where(x => MayWear(x, wearer, clubLevel, owned))
                .OrderByDescending(x => x.Preselectable)
                .ThenBy(x => x.Id)
                .FirstOrDefault();

            if (set is not null)
                worn.Add((type, set, Colors(data, type, set, [], clubLevel)));
        }

        return Write(worn);
    }

    /// <summary>Whether the wearer may put the piece on.</summary>
    public static bool MayWear(
        FigureSet set,
        char wearer,
        int clubLevel,
        IReadOnlySet<int> owned
    ) =>
        set.Selectable
        && (set.Gender == UNISEX || set.Gender == wearer)
        && set.Club <= clubLevel
        && (!set.Sellable || owned.Contains(set.Id));

    /// <summary>
    /// The colours as the piece takes them: no more than it has layers, each one the wearer may
    /// use, a colour they may not swapped for the first they may. A piece that takes no colour
    /// keeps none.
    /// </summary>
    private static List<int> Colors(
        FigureData data,
        FigureSetType type,
        FigureSet set,
        List<int> given,
        int clubLevel
    )
    {
        if (!set.Colorable || !data.Palettes.TryGetValue(type.PaletteId, out var palette))
            return [];

        var fallback = palette.FirstAllowed(clubLevel);
        var colors = new List<int>();

        // A layer left out is drawn as the client draws it (from the first, or the default): only
        // the colours given are checked, up to as many as the piece has layers.
        foreach (var id in given.Take(set.ColorLayers))
        {
            if (
                palette.Colors.TryGetValue(id, out var color)
                && color.Selectable
                && color.Club <= clubLevel
            )
                colors.Add(color.Id);
            else if (fallback is not null)
                colors.Add(fallback.Id);
            else
                break;
        }

        return colors;
    }

    private static string Write(List<(FigureSetType Type, FigureSet Set, List<int> Colors)> worn)
    {
        var figure = new StringBuilder();

        foreach (var (type, set, colors) in worn)
        {
            if (figure.Length > 0)
                figure.Append('.');

            figure
                .Append(type.Type)
                .Append('-')
                .Append(set.Id.ToString(CultureInfo.InvariantCulture));

            foreach (var color in colors)
                figure.Append('-').Append(color.ToString(CultureInfo.InvariantCulture));
        }

        return figure.ToString();
    }

    private static bool TryNumber(string text, out int number) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
}
