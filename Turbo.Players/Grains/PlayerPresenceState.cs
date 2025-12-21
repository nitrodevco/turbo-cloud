using System;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Players.Grains;

[GenerateSerializer]
public sealed class PlayerPresenceState
{
    [Id(0)]
    public required SessionKey SessionKey { get; set; } = string.Empty;

    [Id(1)]
    public required RoomId ActiveRoomId { get; set; } = -1;

    [Id(2)]
    public required RoomId PendingRoomId { get; set; } = -1;

    [Id(3)]
    public required bool PendingRoomApproved { get; set; } = false;

    [Id(4)]
    public required DateTime ActiveRoomSinceUtc { get; set; } = DateTime.UtcNow;
}
