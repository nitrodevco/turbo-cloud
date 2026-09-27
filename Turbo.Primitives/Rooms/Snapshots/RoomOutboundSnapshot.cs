using System.Collections.Generic;
using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Snapshots;

[GenerateSerializer, Immutable]
public sealed record RoomOutboundSnapshot
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    /// <summary>
    /// What one publish sends, in order. A tick that produces several composers (rollers, wired)
    /// publishes them together, so each subscriber gets one stream item and flushes them to its
    /// session in one send rather than one per composer.
    /// </summary>
    [Id(1)]
    public required ImmutableArray<IComposer> Composers { get; init; }

    [Id(2)]
    public List<PlayerId>? ExcludedPlayerIds { get; init; }
}
