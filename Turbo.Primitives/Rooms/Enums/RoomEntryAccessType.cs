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
}
