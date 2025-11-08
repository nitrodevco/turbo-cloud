using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Snapshots.Rooms.StuffData;

[GenerateSerializer, Immutable]
public sealed record StringStuffPayload
{
    [Id(0)]
    public required ImmutableArray<string> Data { get; init; }
}
