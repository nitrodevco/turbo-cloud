namespace Turbo.Primitives.Rooms.Enums;

public enum RoomEntryAccessType
{
    Allowed = 0,
    Banned = 1,
    Full = 2,
    Doorbell = 3,
    PasswordRequired = 4,
    InvalidPassword = 5,

    /// <summary>The room is being deleted and takes no new visitors.</summary>
    Closed = 6,

    /// <summary>
    /// The room holds Builders Club furni that nobody has a membership for any more, so it is
    /// off the navigator and open only to its owner.
    /// </summary>
    HiddenByBuildersClub = 7,
}
