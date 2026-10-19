using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// A post-it pad: an inventory wall item of the post-it category whose legacy stuff data is how
/// many sheets it has left. The client reads the count as <c>int(legacy data)</c>
/// (<c>GroupItem.getTotalCount</c>), keeps placing sheets from the same pad while it says more
/// than one (<c>FurniModel.attemptPlaceNextFurni</c>) and sets the count it is told
/// (<c>FurniModel.updatePostItCount</c>). Each sheet stuck to a wall becomes a note of the pad's
/// own type, so a themed pad (<c>post_it_vd</c>) makes themed notes.
/// </summary>
public static class PostItPads
{
    public static bool IsPad(FurnitureDefinitionSnapshot definition) =>
        definition.FurniCategory == FurnitureCategory.PostIt
        && definition.ProductType == ProductType.Wall;

    /// <summary>
    /// The sheets a pad has left. A pad that names no count (one bought before pads counted their
    /// sheets) or a count below one is a single sheet: the pad itself.
    /// </summary>
    public static int SheetsOf(StuffDataSnapshot stuffData) =>
        int.TryParse(
            (stuffData as LegacyStuffSnapshot)?.Data,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var sheets
        )
        && sheets >= 1
            ? sheets
            : 1;

    /// <summary>The extra data of a pad with <paramref name="sheets"/> left: the count as its legacy state.</summary>
    public static string ExtraData(int sheets) =>
        LegacyExtraData(sheets.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// The extra data of a note stuck to a wall: its <see cref="StickieColors"/> colour and text as
    /// its legacy state. A fresh note is <see cref="StickieColors.DEFAULT"/> and empty.
    /// </summary>
    public static string NoteExtraData(string noteData) => LegacyExtraData(noteData);

    private static string LegacyExtraData(string value) =>
        JsonSerializer.Serialize(
            new Dictionary<string, object> { [ExtraDataSectionType.STUFF] = new { Data = value } }
        );
}
