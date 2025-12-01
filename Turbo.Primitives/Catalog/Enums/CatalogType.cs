using System;

namespace Turbo.Primitives.Catalog.Enums;

public enum CatalogType
{
    Normal = 0,
    BuildersClub = 1,
}

public static class CatalogTypeExtensions
{
    public static string ToLegacyString(this CatalogType catalogType) =>
        catalogType switch
        {
            CatalogType.Normal => "NORMAL",
            CatalogType.BuildersClub => "BUILDERS_CLUB",
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, null),
        };

    public static CatalogType FromLegacyString(this string catalogType) =>
        catalogType switch
        {
            "NORMAL" => CatalogType.Normal,
            "BUILDERS_CLUB" => CatalogType.BuildersClub,
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, null),
        };
}
