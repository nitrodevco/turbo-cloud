using System;

namespace Turbo.Primitives.States.Rooms;

public sealed class RoomState
{
    public bool IsLoaded { get; set; }
    public DateTime LastTick { get; set; }
}
