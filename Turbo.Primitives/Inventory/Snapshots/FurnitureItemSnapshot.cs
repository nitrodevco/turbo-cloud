using Orleans;

namespace Turbo.Primitives.Inventory.Snapshots;

[GenerateSerializer, Immutable]
public abstract record FurnitureItemSnapshot { }
