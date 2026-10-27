using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Rooms.Watching;

/// <summary>
/// What a watcher is shown of a room as their watch starts (<c>IRoomGrain.GetWatchViewAsync</c>):
/// the packets of a room entry that draw the room, taken in one turn of the room grain.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RoomWatchView
{
    /// <summary>
    /// The room's last publish these packets already show
    /// (<c>RoomOutboundSnapshot.Sequence</c>): the watcher keeps only the publishes after it.
    /// </summary>
    [Id(0)]
    public required long Sequence { get; init; }

    /// <summary>In the order a player walking in gets them.</summary>
    [Id(1)]
    public required ImmutableArray<IComposer> Composers { get; init; }
}
