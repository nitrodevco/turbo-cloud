using System.Collections.Immutable;
using Turbo.Contracts.Enums.Furniture;

namespace Turbo.Primitives.Snapshots.Furniture;

public sealed record FurnitureDefinitionSnapshot(
    int Id,
    int SpriteId,
    string PublicName,
    string ProductName
);
