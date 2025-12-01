using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Primitives.Snapshots.Catalog;

public sealed record CatalogProductSnapshot(
    int Id,
    int OfferId,
    ProductType ProductType,
    int FurniDefinitionId,
    int SpriteId,
    string? ExtraParam,
    int Quantity,
    int UniqueSize,
    int UniqueRemaining
);
