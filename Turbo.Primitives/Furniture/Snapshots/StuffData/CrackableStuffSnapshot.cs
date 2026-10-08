using Orleans;

namespace Turbo.Primitives.Furniture.Snapshots.StuffData;

[GenerateSerializer, Immutable]
public sealed record CrackableStuffSnapshot : StuffDataSnapshot
{
    /// <summary>The state the furni draws (<c>furniture_crackable_state</c>).</summary>
    [Id(0)]
    public required string Data { get; init; }

    [Id(1)]
    public required int Hits { get; init; }

    [Id(2)]
    public required int Target { get; init; }
}
