using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Furniture.Snapshots.StuffData;

[GenerateSerializer, Immutable]
public sealed record MapStuffSnapshot : StuffDataSnapshot
{
    [Id(0)]
    public required ImmutableDictionary<string, string> Data { get; init; }
}
