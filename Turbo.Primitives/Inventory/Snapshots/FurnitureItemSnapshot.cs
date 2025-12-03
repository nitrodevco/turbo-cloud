using Orleans;
using Turbo.Primitives.Furniture.Snapshots;

namespace Turbo.Primitives.Inventory.Snapshots;

[GenerateSerializer, Immutable]
public abstract record FurnitureItemSnapshot
{
    [Id(0)]
    public required FurnitureDefinitionSnapshot Definition { get; init; }
}
