using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Orleans.Snapshots.Rooms.StuffData;

[GenerateSerializer, Immutable]
public sealed record MapStuffPayload
{
    [Id(0)]
    public required ImmutableArray<(string key, string value)> Data { get; init; }
}
