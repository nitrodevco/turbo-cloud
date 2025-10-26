using System.Collections.Generic;
using Turbo.Contracts.Enums.Furniture;

namespace Turbo.Primitives.Snapshots.Catalog;

public record CatalogProductSnapshot(
    int Id,
    int OfferId,
    FurniTypeEnum ProductType,
    int? DefinitionId,
    string? ExtraParam,
    int Quantity,
    int UniqueSize,
    int UniqueRemaining
);
