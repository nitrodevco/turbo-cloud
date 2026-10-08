using System.Diagnostics.CodeAnalysis;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// The three room papers: a wallpaper, a floor and a landscape. They are never placed. Used from
/// the inventory in a room (<c>RequestRoomPropertySet</c>), the pattern the item carries
/// (<see cref="ProductStuffData"/>) becomes that room's, and the item is used up. The client
/// picks them out by category, so the server does too.
/// </summary>
public static class RoomDecorations
{
    /// <summary>Which room property an item of this category sets; false for any other furni.</summary>
    public static bool TryGetPropertyType(
        FurnitureCategory category,
        [NotNullWhen(true)] out RoomPropertyType? propertyType
    )
    {
        propertyType = category switch
        {
            FurnitureCategory.WallPaper => RoomPropertyType.Wall,
            FurnitureCategory.Floor => RoomPropertyType.Floor,
            FurnitureCategory.Landscape => RoomPropertyType.Landscape,
            _ => null,
        };

        return propertyType is not null;
    }
}
