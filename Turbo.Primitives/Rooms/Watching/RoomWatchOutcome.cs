namespace Turbo.Primitives.Rooms.Watching;

/// <summary>What asking for a player to watch a room did.</summary>
public enum RoomWatchOutcome
{
    /// <summary>
    /// The player is sent what the room publishes, wrapped in the plugin's envelope, until the
    /// watch ends (renewed when they already watched it for the same plugin).
    /// </summary>
    Watching,

    /// <summary>The player has no session.</summary>
    NotOnline,

    /// <summary>The player is in that room: they get its packets as any player in it does.</summary>
    InThatRoom,

    /// <summary>The player watches as many rooms as they may (<see cref="RoomWatchLimits.MAX_WATCHES_PER_PLAYER"/>).</summary>
    TooManyWatches,

    /// <summary>Another plugin has the player watching that room.</summary>
    WatchedForAnotherPlugin,

    /// <summary>The player is banned from the room.</summary>
    Banned,

    /// <summary>
    /// The room's door would stop the player (doorbell, password, a lobby for new users, a room
    /// closed or hidden), and the watch was not through a linked pair into it.
    /// </summary>
    DoorClosed,

    /// <summary>The room took no hold for the watch (<see cref="RoomHoldOutcome"/> other than Held).</summary>
    NotHeld,
}
