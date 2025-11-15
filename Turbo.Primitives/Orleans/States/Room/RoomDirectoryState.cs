using System;
using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Orleans.States.Room;

[GenerateSerializer]
public sealed class RoomDirectoryState
{
    [Id(0)]
    public required Dictionary<long, RoomActiveInfoState> ActiveRooms { get; set; } = [];

    [Id(1)]
    public required Dictionary<long, int> RoomPopulations { get; set; } = [];

    [Id(2)]
    public required DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
