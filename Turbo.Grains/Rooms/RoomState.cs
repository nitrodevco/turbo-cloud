using System;

namespace Turbo.Grains.Rooms;

public sealed class RoomState
{
    public bool IsLoaded { get; set; }
    public DateTime LastTick { get; set; }
}
