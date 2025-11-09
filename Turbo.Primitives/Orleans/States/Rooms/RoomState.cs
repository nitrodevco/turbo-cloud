using System;
using Orleans;

namespace Turbo.Primitives.Orleans.States.Rooms;

[GenerateSerializer]
public sealed class RoomState
{
    [Id(0)]
    public bool IsLoaded { get; set; }

    [Id(1)]
    public DateTime LastTick { get; set; }
}
