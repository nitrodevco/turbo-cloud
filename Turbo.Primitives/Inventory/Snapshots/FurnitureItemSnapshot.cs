using Orleans;
using Turbo.Primitives.Furniture.Snapshots;

namespace Turbo.Primitives.Inventory.Snapshots;

[GenerateSerializer, Immutable]
public abstract record FurnitureItemSnapshot
{
    [Id(0)]
    public required int OwnerId { get; init; }

    [Id(1)]
    public required string OwnerName { get; init; }

    [Id(2)]
    public required FurnitureDefinitionSnapshot Definition { get; init; }
}
