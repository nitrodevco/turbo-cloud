using System.Collections.Immutable;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Primitives.Pets;

/// <summary>
/// A monsterplant's look. The client's monsterplant asset draws its body, mouth, nose and eyes
/// on layers 1 to 4, each in one of twelve part types (1 to 12), coloured by one of eleven
/// palettes (<c>mnstr_pal1</c> to <c>mnstr_pal11</c>, ids 0 to 10). The body part is the
/// plant's shape and its palette the plant's colour, read by <c>~plant.shape</c> and
/// <c>~plant.color</c>.
/// </summary>
public static class MonsterplantFigure
{
    public const int BODY_LAYER = 1;
    public const int MIN_SHAPE = 1;
    public const int MAX_SHAPE = 12;

    /// <summary>The body part a plant of this shape and colour is drawn with.</summary>
    public static ImmutableArray<int> CustomParts(int shape, int color) =>
        [BODY_LAYER, shape, color];

    /// <summary>The body part type, or 0 for a plant drawn with the asset's default body.</summary>
    public static int Shape(PetFigureSnapshot figure) =>
        TryGetBody(figure, out var shape, out _) ? shape : 0;

    /// <summary>The body's palette; the figure's palette when the body has none of its own.</summary>
    public static int Color(PetFigureSnapshot figure) =>
        TryGetBody(figure, out _, out var color) && color >= 0 ? color : figure.PaletteId;

    private static bool TryGetBody(PetFigureSnapshot figure, out int shape, out int color)
    {
        var parts = figure.CustomParts;

        for (
            var i = 0;
            i + PetFigure.CUSTOM_PART_FIELDS <= parts.Length;
            i += PetFigure.CUSTOM_PART_FIELDS
        )
        {
            if (parts[i] != BODY_LAYER || parts[i + 1] < MIN_SHAPE)
                continue;

            shape = parts[i + 1];
            color = parts[i + 2];

            return true;
        }

        shape = 0;
        color = 0;

        return false;
    }
}
