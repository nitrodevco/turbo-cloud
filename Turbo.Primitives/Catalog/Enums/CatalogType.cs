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

    /// <summary>
    /// Reads the catalog name a client sent. Anything else is the normal catalog rather than an
    /// exception: this runs in a parser on a string the client chose, and a client that sends
    /// nonsense would otherwise be able to fill the log one packet at a time.
    /// </summary>
    public static CatalogType FromLegacyString(this string? catalogType) =>
        catalogType switch
        {
            "BUILDERS_CLUB" => CatalogType.BuildersClub,
            _ => CatalogType.Normal,
        };
}
