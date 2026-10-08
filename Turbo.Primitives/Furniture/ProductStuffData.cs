using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots.StuffData;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// Furni whose catalog product names what the item is: a wallpaper, floor or landscape pattern
/// (<c>101</c>, <c>1.1</c>), a poster (<c>12</c>, drawn as <c>poster12</c>) or a song disc's
/// song id. One definition serves every pattern, poster or song; the product's extra parameter
/// tells them apart, and it is written onto the item as its legacy stuff data when the item is
/// bought, so the inventory, a room and a trade all read it from the item. The client's own
/// purchase parameter is not used for these: the offer decides what was sold.
/// </summary>
public static class ProductStuffData
{
    /// <summary>Whether items of this category carry their product's extra parameter.</summary>
    public static bool IsNamedByProduct(FurnitureCategory category) =>
        category
            is FurnitureCategory.WallPaper
                or FurnitureCategory.Floor
                or FurnitureCategory.Landscape
                or FurnitureCategory.Poster
                or FurnitureCategory.TraxSong;

    /// <summary>
    /// Whether a product's extra parameter names something an item of this category can be: a
    /// song disc needs a song id, the others any value at all.
    /// </summary>
    public static bool IsValid(FurnitureCategory category, string? value) =>
        category == FurnitureCategory.TraxSong
            ? TryParseSongId(value, out _)
            : !string.IsNullOrWhiteSpace(value);

    /// <summary>The extra data a newly bought item starts with: the value as its legacy state.</summary>
    public static string ExtraData(string value) =>
        JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                [ExtraDataSectionType.STUFF] = new { Data = value.Trim() },
            }
        );

    /// <summary>What an item carries: its legacy stuff data, when it has any.</summary>
    public static bool TryGetValue(StuffDataSnapshot stuffData, out string value)
    {
        value = (stuffData as LegacyStuffSnapshot)?.Data ?? string.Empty;

        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// The song a song disc plays, from the stuff data of the item (an inventory snapshot's
    /// <c>StuffData</c> or a room item's snapshot). False for anything that names no song.
    /// </summary>
    public static bool TryGetSongId(StuffDataSnapshot stuffData, out int songId)
    {
        songId = 0;

        return TryGetValue(stuffData, out var value) && TryParseSongId(value, out songId);
    }

    /// <summary>A song id as a product or an item's legacy data holds it: a whole number above zero.</summary>
    public static bool TryParseSongId(string? value, out int songId) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out songId)
        && songId > 0;
}
