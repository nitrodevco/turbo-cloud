namespace Turbo.Primitives.Rooms.Enums;

public enum RoomDoorModeType
{
    Open = 0,
    Locked = 1,
    Password = 2,
    Invisible = 3,

    /// <summary>
    /// Habbo's new user lobby: only new users (<c>NoobnessLevel</c> 2), staff and whoever has
    /// rights get in, and the client's "Enter the room!" offer and toolbar send new users to one.
    /// Set with <c>:nooblobby</c>; the room settings window does not offer it.
    /// </summary>
    NoobLobby = 4,

    /// <summary>
    /// Only the owner's friends get in, beside the owner, whoever has rights and staff
    /// (<c>roomsettings.access_rights.friends</c>, the room settings window's choice from client
    /// build 116). The room stays listed; the client asks nothing before it tries the door. The
    /// client's 5 and 6 are not door modes a room is saved with.
    /// </summary>
    FriendsOnly = 7,
}
