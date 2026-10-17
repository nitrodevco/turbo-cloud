using System;
using System.Collections.Immutable;
using System.Globalization;

namespace Turbo.Primitives.Pets;

/// <summary>
/// What a pet product does, read from its definition's <c>customparams</c> as Habbo's furni data
/// gives them and the client's use-product preview reads them
/// (<c>UseProductConfirmationView</c>): a body dye is <c>&lt;petType&gt; &lt;colourTag&gt;</c>
/// (<c>horse_dye_08</c> "15 Gold"), a hair dye <c>&lt;petType&gt; &lt;layers&gt; &lt;palettes&gt;</c>
/// (<c>horse_hairdye_06</c> "15 2,3 54,54") and a hair style
/// <c>&lt;petType&gt; &lt;layers&gt; &lt;parts&gt; &lt;palettes&gt;</c> (<c>horse_hairstyle_3</c>
/// "15 2,3 103,103 1,1"), the lists comma separated and paired by position. A saddle is a hair
/// style followed by the effect its rider wears (<c>horse_saddle1</c> "15 4 9 0 77",
/// <c>horse_saddle2</c> "15 4 10 0 103").
/// </summary>
public static class PetProductParams
{
    /// <summary>
    /// The colour tag names by number, as the client's <c>PetColorResult.COLOR_TAGS</c> lists
    /// them; a pet palette's <c>colortag</c> (and <c>pet_breeds.color_tag</c>) is the index.
    /// </summary>
    public static readonly ImmutableArray<string> COLOR_TAGS =
    [
        "Null",
        "Black",
        "White",
        "Grey",
        "Red",
        "Orange",
        "Pink",
        "Green",
        "Lime",
        "Blue",
        "Light-Blue",
        "Dark-Blue",
        "Yellow",
        "Brown",
        "Dark-Brown",
        "Beige",
        "Cyan",
        "Purple",
        "Gold",
    ];

    /// <summary>A body dye's pet type and colour tag number, or null when the params are not one.</summary>
    public static (int PetTypeId, int ColorTag)? ParseDye(string? customParams)
    {
        var fields = Split(customParams);

        if (fields.Length < 2 || !TryInt(fields[0], out var petType))
            return null;

        // The client matches the name exactly (`PetColorResult.tag == param`).
        var tag = COLOR_TAGS.IndexOf(fields[1]);

        return tag < 0 ? null : (petType, tag);
    }

    /// <summary>The pet type a hair dye or hair style fits, or null when the params are not one.</summary>
    public static int? PetTypeOfCustomParts(string? customParams)
    {
        var fields = Split(customParams);

        return fields.Length is 3 or 4 && TryInt(fields[0], out var petType) ? petType : null;
    }

    /// <summary>
    /// The pet's custom parts after a hair dye or hair style is used, or null when the params are
    /// neither. A dye recolours each listed layer and keeps its part (none, -1, when the pet has
    /// none there); a style sets each listed layer's part and keeps the colour the layer already
    /// has, taking the given one only for a layer the pet has no part on - as the preview builds it.
    /// Layers not listed are left as they are.
    /// </summary>
    public static ImmutableArray<int>? ApplyCustomParts(
        ImmutableArray<int> current,
        string? customParams
    )
    {
        var fields = Split(customParams);

        if (fields.Length is not (3 or 4))
            return null;

        var isStyle = fields.Length == 4;
        var layers = SplitList(fields[1]);
        var parts = isStyle ? SplitList(fields[2]) : null;
        var palettes = SplitList(fields[isStyle ? 3 : 2]);

        if (
            layers is null
            || palettes is null
            || layers.Length == 0
            || palettes.Length != layers.Length
            || (isStyle && (parts is null || parts.Length != layers.Length))
        )
            return null;

        var result = current.ToBuilder();

        for (var i = 0; i < layers.Length; i++)
        {
            var at = IndexOfLayer(result, layers[i]);
            var part = isStyle ? parts![i] : (at >= 0 ? result[at + 1] : -1);
            var palette = isStyle && at >= 0 ? result[at + 2] : palettes[i];

            if (at < 0)
            {
                result.Add(layers[i]);
                result.Add(part);
                result.Add(palette);

                continue;
            }

            result[at + 1] = part;
            result[at + 2] = palette;
        }

        return result.ToImmutable();
    }

    /// <summary>
    /// A saddle's pet type, parts and rider effect, or null when the params are not one. Its parts
    /// are drawn on the pet as given, as the client's preview of the saddle draws them
    /// (<c>UseProductConfirmationView</c>, the saddle page).
    /// </summary>
    public static PetSaddleParams? ParseSaddle(string? customParams)
    {
        var fields = Split(customParams);

        if (
            fields.Length != 5
            || !TryInt(fields[0], out var petType)
            || !TryInt(fields[4], out var effectId)
        )
            return null;

        var layers = SplitList(fields[1]);
        var parts = SplitList(fields[2]);
        var palettes = SplitList(fields[3]);

        if (
            layers is null
            || parts is null
            || palettes is null
            || layers.Length == 0
            || parts.Length != layers.Length
            || palettes.Length != layers.Length
        )
            return null;

        var customParts = ImmutableArray.CreateBuilder<int>(
            layers.Length * PetFigure.CUSTOM_PART_FIELDS
        );

        for (var i = 0; i < layers.Length; i++)
        {
            customParts.Add(layers[i]);
            customParts.Add(parts[i]);
            customParts.Add(palettes[i]);
        }

        return new PetSaddleParams(petType, customParts.ToImmutable(), effectId);
    }

    /// <summary>The pet's custom parts with the saddle's on: its layers replaced, the rest kept.</summary>
    public static ImmutableArray<int> WithSaddle(
        ImmutableArray<int> current,
        PetSaddleParams saddle
    )
    {
        var result = WithoutSaddle(current, saddle).ToBuilder();

        result.AddRange(saddle.CustomParts);

        return result.ToImmutable();
    }

    /// <summary>The pet's custom parts with the saddle's layers taken off.</summary>
    public static ImmutableArray<int> WithoutSaddle(
        ImmutableArray<int> current,
        PetSaddleParams saddle
    )
    {
        var result = ImmutableArray.CreateBuilder<int>(current.Length);

        for (
            var i = 0;
            i + PetFigure.CUSTOM_PART_FIELDS <= current.Length;
            i += PetFigure.CUSTOM_PART_FIELDS
        )
        {
            if (IsSaddleLayer(saddle, current[i]))
                continue;

            result.Add(current[i]);
            result.Add(current[i + 1]);
            result.Add(current[i + 2]);
        }

        return result.ToImmutable();
    }

    private static bool IsSaddleLayer(PetSaddleParams saddle, int layer)
    {
        for (
            var i = 0;
            i + PetFigure.CUSTOM_PART_FIELDS <= saddle.CustomParts.Length;
            i += PetFigure.CUSTOM_PART_FIELDS
        )
        {
            if (saddle.CustomParts[i] == layer)
                return true;
        }

        return false;
    }

    private static int IndexOfLayer(ImmutableArray<int>.Builder parts, int layer)
    {
        for (
            var i = 0;
            i + PetFigure.CUSTOM_PART_FIELDS <= parts.Count;
            i += PetFigure.CUSTOM_PART_FIELDS
        )
        {
            if (parts[i] == layer)
                return i;
        }

        return -1;
    }

    private static string[] Split(string? customParams) =>
        (customParams ?? string.Empty).Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

    private static int[]? SplitList(string field)
    {
        var items = field.Split(',', StringSplitOptions.TrimEntries);
        var values = new int[items.Length];

        for (var i = 0; i < items.Length; i++)
        {
            if (!TryInt(items[i], out values[i]))
                return null;
        }

        return values;
    }

    private static bool TryInt(string value, out int result) =>
        int.TryParse(
            value,
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out result
        );
}
