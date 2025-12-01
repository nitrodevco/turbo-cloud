using System;

namespace Turbo.Primitives.Furniture.Enums;

public enum ProductType
{
    Floor = 0,
    Wall = 1,
    Effect = 2,
    Badge = 3,
    Robot = 4,
    HabboClub = 5,
    Pet = 6,
}

public static class ProductTypeEnumExtension
{
    public static string ToLegacyString(this ProductType productType) =>
        productType switch
        {
            ProductType.Floor => "s",
            ProductType.Wall => "i",
            ProductType.Effect => "e",
            ProductType.Badge => "b",
            ProductType.Robot => "r",
            ProductType.HabboClub => "h",
            ProductType.Pet => "p",
            _ => throw new ArgumentOutOfRangeException(nameof(productType), productType, null),
        };

    public static ProductType FromLegacyString(this string productType) =>
        productType switch
        {
            "s" => ProductType.Floor,
            "i" => ProductType.Wall,
            "e" => ProductType.Effect,
            "b" => ProductType.Badge,
            "r" => ProductType.Robot,
            "h" => ProductType.HabboClub,
            "p" => ProductType.Pet,
            _ => throw new ArgumentOutOfRangeException(nameof(productType), productType, null),
        };
}
