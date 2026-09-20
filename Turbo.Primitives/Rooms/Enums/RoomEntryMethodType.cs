namespace Turbo.Primitives.Rooms.Enums;

/// <summary>
/// How a player came to be in the room, as the wired <c>@room_entry.method</c> variable reports
/// it. One is not used: the numbering is the hotel's and the gap is left as it is so the three
/// that are used keep the numbers a wired stack already reads.
/// </summary>
public enum RoomEntryMethodType
{
    /// <summary>Walked in: the navigator, a friend, a link, anything that is not a furni.</summary>
    Default = 0,

    /// <summary>Through a teleporter, which is paired with another one somewhere.</summary>
    Teleport = 2,

    /// <summary>Through a furni that leads to one fixed room.</summary>
    RoomNetwork = 3,
}
