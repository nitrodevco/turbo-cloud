using System;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Primitives.Orleans.States.Rooms;

[GenerateSerializer]
public sealed class PlayerPresenceState
{
    [Id(0)]
    public required SessionKey SessionKey { get; set; } = SessionKey.Empty;

    [Id(1)]
    public required long ActiveRoomId { get; set; } = -1;

    [Id(2)]
    public required DateTimeOffset Since { get; set; }

    [Id(3)]
    public required long PendingRoomId { get; set; } = -1;

    [Id(4)]
    public required bool PendingRoomApproved { get; set; } = false;
}
