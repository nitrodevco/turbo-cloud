using System;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Players.Grains;

public sealed class PlayerPresenceLiveState
{
    public required PlayerId PlayerId { get; init; }
    public RoomId ActiveRoomId { get; set; } = -1;
    public RoomId PendingRoomId { get; set; } = -1;
    public bool PendingRoomApproved { get; set; } = false;
    public DateTime ActiveRoomSinceUtc { get; set; } = DateTime.UtcNow;
}
