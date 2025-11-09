using System;
using Orleans;

namespace Turbo.Primitives.States.Rooms;

[GenerateSerializer]
public sealed class PlayerPresenceState
{
    [Id(0)]
    public long RoomId { get; set; } = -1;

    [Id(1)]
    public DateTimeOffset Since { get; set; }

    [Id(2)]
    public long PendingRoomId { get; set; } = -1;

    [Id(3)]
    public bool PendingRoomApproved { get; set; } = false;
}
