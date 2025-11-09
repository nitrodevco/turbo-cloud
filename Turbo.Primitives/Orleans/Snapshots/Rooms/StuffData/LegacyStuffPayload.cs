using Orleans;

namespace Turbo.Primitives.Orleans.Snapshots.Rooms.StuffData;

[GenerateSerializer, Immutable]
public sealed record LegacyStuffPayload
{
    [Id(0)]
    public required string LegacyString { get; init; }
}
