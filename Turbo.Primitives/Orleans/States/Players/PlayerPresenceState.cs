using System;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Session;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Orleans.States.Players;

[GenerateSerializer]
public sealed class PlayerPresenceState
{
    [Id(0)]
    public required SessionKey Session { get; set; } = SessionKey.Empty;

    [Id(1)]
    public required RoomId ActiveRoomId { get; set; } = RoomId.Empty;

    [Id(2)]
    public required RoomId PendingRoomId { get; set; } = RoomId.Empty;

    [Id(3)]
    public required bool PendingRoomApproved { get; set; } = false;

    [Id(4)]
    public required DateTime ActiveRoomSinceUtc { get; set; } = DateTime.UtcNow;
}
