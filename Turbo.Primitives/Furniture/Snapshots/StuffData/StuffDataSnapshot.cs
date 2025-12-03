using Orleans;

namespace Turbo.Primitives.Furniture.Snapshots.StuffData;

[GenerateSerializer, Immutable]
public abstract record StuffDataSnapshot
{
    [Id(0)]
    public required int StuffBitmask { get; init; }

    [Id(1)]
    public int UniqueNumber { get; init; }

    [Id(2)]
    public int UniqueSeries { get; init; }
}
