using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Primitives.Snapshots.Catalog;

public sealed record CatalogFrontPageItemSnapshot(
    int Position,
    string ItemName,
    string ItemPromoImage,
    CatalogFrontPageItemType Type,
    string? CatalogPageLocation,
    int? ProductOfferId,
    string? ProductCode,
    int ExpiresInSeconds
);
