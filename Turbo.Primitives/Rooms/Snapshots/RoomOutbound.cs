using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Snapshots;

[GenerateSerializer, Immutable]
public sealed record RoomOutbound
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    [Id(1)]
    public required IComposer Composer { get; init; }

    [Id(2)]
    public List<PlayerId>? ExcludedPlayerIds { get; init; }
}
