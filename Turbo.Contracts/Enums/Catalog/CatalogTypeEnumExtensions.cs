using System;

namespace Turbo.Contracts.Enums.Catalog;

public static class CatalogTypeEnumExtensions
{
    public static string ToLegacyString(this CatalogTypeEnum catalogType) =>
        catalogType switch
        {
            CatalogTypeEnum.Normal => "NORMAL",
            CatalogTypeEnum.BuildersClub => "BUILDERS_CLUB",
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, null),
        };

    public static CatalogTypeEnum FromLegacyString(this string catalogType) =>
        catalogType switch
        {
            "NORMAL" => CatalogTypeEnum.Normal,
            "BUILDERS_CLUB" => CatalogTypeEnum.BuildersClub,
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, null),
        };
}
