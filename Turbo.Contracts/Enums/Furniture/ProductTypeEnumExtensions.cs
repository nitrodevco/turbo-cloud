using System;

namespace Turbo.Contracts.Enums.Furniture;

public static class ProductTypeEnumExtension
{
    public static string ToLegacyString(this ProductTypeEnum productType) =>
        productType switch
        {
            ProductTypeEnum.Floor => "s",
            ProductTypeEnum.Wall => "i",
            ProductTypeEnum.Effect => "e",
            ProductTypeEnum.Badge => "b",
            ProductTypeEnum.Robot => "r",
            ProductTypeEnum.HabboClub => "h",
            ProductTypeEnum.Pet => "p",
            _ => throw new ArgumentOutOfRangeException(nameof(productType), productType, null),
        };

    public static ProductTypeEnum FromLegacyString(this string productType) =>
        productType switch
        {
            "s" => ProductTypeEnum.Floor,
            "i" => ProductTypeEnum.Wall,
            "e" => ProductTypeEnum.Effect,
            "b" => ProductTypeEnum.Badge,
            "r" => ProductTypeEnum.Robot,
            "h" => ProductTypeEnum.HabboClub,
            "p" => ProductTypeEnum.Pet,
            _ => throw new ArgumentOutOfRangeException(nameof(productType), productType, null),
        };
}
