using Orleans;

namespace Turbo.Primitives.Furniture.Snapshots.WiredData;

[GenerateSerializer, Immutable]
public record WiredDataTriggerSnapshot : WiredDataSnapshot { }
