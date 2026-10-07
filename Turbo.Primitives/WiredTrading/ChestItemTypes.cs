using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.WiredTrading;

/// <summary>
/// The type a chest counts a furni under, as the client builds it: its sprite, wall or floor,
/// and for a poster which poster, because every poster shares one sprite.
/// </summary>
public static class ChestItemTypes
{
    /// <summary>The definition name every legacy poster shares; its stuff data names the poster.</summary>
    public const string POSTER_NAME = "poster";

    public static ChestItemTypeSnapshot Of(
        FurnitureDefinitionSnapshot definition,
        StuffDataSnapshot stuffData
    ) =>
        new()
        {
            IsWallItem = definition.ProductType == ProductType.Wall,
            TypeId = definition.SpriteId,
            LegacyPosterId =
                definition.Name == POSTER_NAME && stuffData is LegacyStuffSnapshot legacy
                    ? legacy.Data
                    : string.Empty,
        };
}
