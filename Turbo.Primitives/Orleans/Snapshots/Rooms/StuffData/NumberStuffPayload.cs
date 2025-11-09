using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Orleans.Snapshots.Rooms.StuffData;

[GenerateSerializer, Immutable]
public sealed record NumberStuffPayload
{
    [Id(0)]
    public required ImmutableArray<int> Data { get; init; }
}
