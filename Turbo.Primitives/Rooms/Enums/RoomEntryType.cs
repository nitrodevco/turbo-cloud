namespace Turbo.Primitives.Rooms.Enums;

/// <summary>
/// How a player is attempting to enter a room. Decides which door checks apply and whether the
/// server should stay silent when the client is expected to prompt the player first.
/// </summary>
public enum RoomEntryType
{
    /// <summary>
    /// The client picked the room in the navigator (GetGuestRoom with roomForward). A locked or
    /// password-protected door is left to the client to prompt for; nothing is sent.
    /// </summary>
    Navigator = 0,

    /// <summary>
    /// The client asked to enter directly (OpenFlatConnection: home room button, password submit,
    /// doorbell ring). Door checks apply and failures are reported back.
    /// </summary>
    Direct = 1,

    /// <summary>
    /// The server is moving the player (teleporter, wired, moderation). Door mode is bypassed;
    /// bans and room capacity still apply.
    /// </summary>
    Forced = 2,
}
