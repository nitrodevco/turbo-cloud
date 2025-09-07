using System;

namespace Turbo.Contracts.Enums.Furniture;

public static class FurniTypeEnumExtension
{
    public static string ToLegacyString(FurniTypeEnum furniType) =>
        furniType switch
        {
            FurniTypeEnum.Floor => "s",
            FurniTypeEnum.Wall => "i",
            FurniTypeEnum.Effect => "e",
            FurniTypeEnum.Badge => "b",
            FurniTypeEnum.Robot => "r",
            FurniTypeEnum.HabboClub => "h",
            FurniTypeEnum.Pet => "p",
            _ => throw new ArgumentOutOfRangeException(nameof(furniType), furniType, null),
        };

    public static FurniTypeEnum? FromLegacyString(string furniType) =>
        furniType switch
        {
            "s" => FurniTypeEnum.Floor,
            "i" => FurniTypeEnum.Wall,
            "e" => FurniTypeEnum.Effect,
            "b" => FurniTypeEnum.Badge,
            "r" => FurniTypeEnum.Robot,
            "h" => FurniTypeEnum.HabboClub,
            "p" => FurniTypeEnum.Pet,
            _ => null,
        };
}
