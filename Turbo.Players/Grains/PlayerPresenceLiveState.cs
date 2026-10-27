using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Orleans.Streams;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Players.Grains;

internal sealed class PlayerPresenceLiveState
{
    public required PlayerId PlayerId { get; init; }
    public SessionKey SessionKey { get; set; } = SessionKey.Invalid;
    public long SessionGeneration { get; set; }
    public DateTime? AchievementOnlineSinceUtc { get; set; }

    /// <summary>When the session attached now began; null with none.</summary>
    public DateTime? SessionStartedUtc { get; set; }

    /// <summary>
    /// Protocol extensions the current session accepted, by name, at the agreed version. Cleared
    /// with every session change.
    /// </summary>
    public ImmutableDictionary<string, int> ClientCapabilities { get; set; } =
        ImmutableDictionary<string, int>.Empty;
    public RoomId ActiveRoomId { get; set; } = -1;
    public RoomId PendingRoomId { get; set; } = -1;
    public RoomEntryState PendingRoomState { get; set; } = RoomEntryState.None;

    /// <summary>How a furni said the player would arrive, and the room it said it for.</summary>
    public RoomId PendingEntryRoomId { get; set; } = -1;
    public RoomEntrySnapshot PendingEntry { get; set; } = RoomEntrySnapshot.Default;
    public DateTime PendingEntrySetAtUtc { get; set; }
    public DateTime ActiveRoomSinceUtc { get; set; } = DateTime.UtcNow;

    /// <summary>The rooms the player watches for a plugin, besides the one they are in.</summary>
    public Dictionary<RoomId, WatchedRoom> Watches { get; } = [];

    /// <summary>Composers waiting for the session, in the order they were sent.</summary>
    public Queue<IComposer> OutgoingQueue { get; } = new();
    public bool IsProcessingQueue { get; set; }
}

/// <summary>
/// A room the player watches: for which plugin, in which envelope, until when, and the stream
/// subscription that brings its packets.
/// </summary>
internal sealed class WatchedRoom
{
    public required string Plugin { get; init; }
    public required PluginMessageComposer Envelope { get; set; }
    public required DateTime ExpiresAtUtc { get; set; }
    public StreamSubscriptionHandle<RoomOutboundSnapshot>? Subscription { get; set; }

    /// <summary>
    /// The room's publishes that arrive while its first view is on its way, held until the view
    /// is queued; null once it is.
    /// </summary>
    public List<RoomOutboundSnapshot>? AwaitingView { get; set; } = [];

    /// <summary>
    /// The last publish the first view already shows: one up to it that arrives late is dropped.
    /// Null once a later one has arrived, after which every publish is the room's next.
    /// </summary>
    public long? ShownUpTo { get; set; }
}
