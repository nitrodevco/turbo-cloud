using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.StuffData;

[GenerateSerializer, Immutable]
public sealed record LegacyStuffPayload
{
    [Id(0)]
    public required string LegacyString { get; init; }
}
