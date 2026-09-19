using System;
using System.Collections.Generic;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Players.Grains;

internal sealed class PlayerPresenceLiveState
{
    public required PlayerId PlayerId { get; init; }
    public RoomId ActiveRoomId { get; set; } = -1;
    public RoomId PendingRoomId { get; set; } = -1;
    public RoomEntryState PendingRoomState { get; set; } = RoomEntryState.None;
    public DateTime ActiveRoomSinceUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Composers waiting for the session, in the order they were sent.</summary>
    public Queue<IComposer> OutgoingQueue { get; } = new();
    public bool IsProcessingQueue { get; set; }
}
