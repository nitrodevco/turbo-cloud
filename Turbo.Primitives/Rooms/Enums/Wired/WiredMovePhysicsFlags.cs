using System;

namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>The four checkboxes of the "movement physics" addon.</summary>
[Flags]
public enum WiredMovePhysicsFlags
{
    None = 0,
    KeepAltitude = 1 << 0,
    MoveThroughFurni = 1 << 1,
    MoveThroughUsers = 1 << 2,
    BlockedByFurni = 1 << 3,
}
