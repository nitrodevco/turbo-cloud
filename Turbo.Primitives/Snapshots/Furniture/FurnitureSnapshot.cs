using System.Collections.Immutable;

namespace Turbo.Primitives.Snapshots.Furniture;

public sealed record FurnitureSnapshot(
    ImmutableDictionary<int, FurnitureDefinitionSnapshot> DefinitionsById
);
