using Orleans;

namespace Turbo.Primitives.Inventory.Snapshots;

[GenerateSerializer, Immutable]
public sealed record FurnitureFloorItemSnapshot : FurnitureItemSnapshot;
