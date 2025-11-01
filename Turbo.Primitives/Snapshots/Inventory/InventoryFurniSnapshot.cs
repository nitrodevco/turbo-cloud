namespace Turbo.Primitives.Snapshots.Inventory;

public sealed record InventoryFurniSnapshot(
    int Id,
    int PlayerId,
    int DefinitionId,
    string? StuffData
);
