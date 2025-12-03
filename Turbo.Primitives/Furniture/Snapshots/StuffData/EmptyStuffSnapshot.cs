using Orleans;

namespace Turbo.Primitives.Furniture.Snapshots.StuffData;

[GenerateSerializer, Immutable]
public sealed record EmptyStuffSnapshot : StuffDataSnapshot { }
