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
}
