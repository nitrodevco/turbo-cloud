using Orleans;

namespace Turbo.Primitives.Furniture.Snapshots.WiredData;

[GenerateSerializer, Immutable]
public record WiredDataActionSnapshot : WiredDataSnapshot
{
    [Id(0)]
    public required int DelayInPulses { get; init; }
}
