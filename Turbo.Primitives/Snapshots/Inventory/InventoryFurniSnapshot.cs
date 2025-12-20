using Turbo.Primitives.Players;

namespace Turbo.Primitives.Snapshots.Inventory;

public sealed record InventoryFurniSnapshot(
    int Id,
    PlayerId PlayerId,
    int DefinitionId,
    string? StuffData
);
