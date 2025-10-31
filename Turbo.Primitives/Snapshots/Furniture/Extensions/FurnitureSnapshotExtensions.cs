namespace Turbo.Primitives.Snapshots.Furniture.Extensions;

public static class FurnitureSnapshotExtensions
{
    public static FurnitureDefinitionSnapshot? GetDefinitionById(
        this FurnitureSnapshot snapshot,
        int definitionId
    )
    {
        return snapshot.DefinitionsById.TryGetValue(definitionId, out var definition)
            ? definition
            : null;
    }
}
