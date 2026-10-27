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

    /// <summary>Optional diagnostics timestamp; zero when disabled or sent by an older silo.</summary>
    [Id(3)]
    public long PublishedAtUtcTicks { get; init; }

    /// <summary>
    /// The hold this publish ends once its composers are queued: that player's direct composers
    /// held behind it go out after it (see <see cref="OutboundHold"/>).
    /// </summary>
    [Id(4)]
    public OutboundHold ReleasesHold { get; init; }

    /// <summary>
    /// The room's count of its publishes, this one included: a watcher's first view of the room
    /// (<c>IRoomGrain.GetWatchViewAsync</c>) names the last publish it already shows, so the
    /// watcher drops what came before it and keeps what came after.
    /// </summary>
    [Id(5)]
    public long Sequence { get; init; }
}
