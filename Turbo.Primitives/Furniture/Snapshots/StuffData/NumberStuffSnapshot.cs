using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Furniture.Snapshots.StuffData;

[GenerateSerializer, Immutable]
public sealed record NumberStuffSnapshot : StuffDataSnapshot
{
    [Id(0)]
    public required ImmutableArray<int> Data { get; init; }
}
