namespace Turbo.Primitives.Rooms.Enums;

/// <summary>
/// Where a player is in the process of entering a room they are not yet inside.
/// </summary>
public enum RoomEntryState
{
    None = 0,
    RingingDoorbell = 1,
    Approved = 2,
}
