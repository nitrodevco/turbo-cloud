using System;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Primitives.Orleans.States.Room;

[GenerateSerializer]
public sealed class RoomState
{
    [Id(0)]
    public required RoomSnapshot RoomSnapshot { get; set; }

    [Id(1)]
    public required int ModelId { get; set; }

    [Id(2)]
    public required bool IsLoaded { get; set; } = false;

    [Id(3)]
    public required DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
