using System;

namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// Who may read or modify a room's wired. Bit positions match the checkbox ids in the client's
/// wired settings tab: the owner is always allowed and is not a bit; <see cref="Everyone"/> is
/// only offered for reading.
/// </summary>
[Flags]
public enum WiredPermissionFlags
{
    None = 0,
    Everyone = 1 << 0,
    Rights = 1 << 1,
    GroupMembers = 1 << 2,
    GroupAdmins = 1 << 3,
}
