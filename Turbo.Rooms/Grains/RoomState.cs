using System;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Rooms.Grains;

[GenerateSerializer]
public sealed class RoomState
{
    [Id(0)]
    public required RoomSnapshot RoomSnapshot { get; set; }

    [Id(1)]
    public required bool IsLoaded { get; set; } = false;

    [Id(2)]
    public required DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
