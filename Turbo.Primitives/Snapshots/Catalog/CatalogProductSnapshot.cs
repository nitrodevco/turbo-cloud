using System.Collections.Generic;
using Turbo.Contracts.Enums.Furniture;

namespace Turbo.Primitives.Snapshots.Catalog;

public record CatalogProductSnapshot(
    int Id,
    int OfferId,
    ProductTypeEnum ProductType,
    int FurniDefinitionId,
    int SpriteId,
    string ExtraParam,
    int Quantity,
    int UniqueSize,
    int UniqueRemaining
);
