using Turbo.Contracts.Enums.Catalog;

namespace Turbo.Primitives.Snapshots.Catalog;

public sealed record CatalogFrontPageItemSnapshot(
    int Position,
    string ItemName,
    string ItemPromoImage,
    CatalogFrontPageItemTypeEnum Type,
    string? CatalogPageLocation,
    int? ProductOfferId,
    string? ProductCode,
    int ExpiresInSeconds
);
