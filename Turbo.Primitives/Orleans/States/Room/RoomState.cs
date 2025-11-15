using System;
using Orleans;

namespace Turbo.Primitives.Orleans.States.Room;

[GenerateSerializer]
public sealed class RoomState
{
    [Id(0)]
    public required bool IsLoaded { get; set; } = false;

    [Id(1)]
    public required DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
