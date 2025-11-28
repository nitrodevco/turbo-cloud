using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.StuffData;

[GenerateSerializer, Immutable]
public sealed record VoteStuffPayload
{
    [Id(0)]
    public required string Data { get; init; }

    [Id(1)]
    public required int Result { get; init; }
}
