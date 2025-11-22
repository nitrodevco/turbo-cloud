using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Orleans.Snapshots.Room.StuffData;

[GenerateSerializer, Immutable]
public sealed record MapStuffPayload
{
    [Id(0)]
    public required ImmutableDictionary<string, string> Data { get; init; }
}
