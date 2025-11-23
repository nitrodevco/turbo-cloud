using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Primitives.Actor;

[GenerateSerializer, Immutable]
public sealed record ActorContext
{
    [Id(0)]
    public required ActorOrigin Origin { get; init; }

    [Id(1)]
    public SessionKey SessionKey { get; init; } = SessionKey.Empty;

    [Id(2)]
    public long PlayerId { get; init; } = -1;

    [Id(3)]
    public long RoomId { get; init; } = -1;
}
