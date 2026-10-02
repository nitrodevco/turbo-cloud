using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
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

    /// <summary>Composers waiting for the session, in the order they were sent.</summary>
    public Queue<IComposer> OutgoingQueue { get; } = new();
    public bool IsProcessingQueue { get; set; }
}
