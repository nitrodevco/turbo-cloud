using Orleans;

namespace Turbo.Primitives.Furniture.Snapshots.StuffData;

[GenerateSerializer, Immutable]
public sealed record VoteStuffSnapshot : StuffDataSnapshot
{
    [Id(0)]
    public required string Data { get; init; }

    [Id(1)]
    public required int Result { get; init; }
}
