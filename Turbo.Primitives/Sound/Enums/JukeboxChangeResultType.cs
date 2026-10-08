namespace Turbo.Primitives.Sound.Enums;

/// <summary>How a change to a jukebox's playlist went.</summary>
public enum JukeboxChangeResultType
{
    Done,

    /// <summary>The playlist already holds as many disks as a jukebox takes; the client says so.</summary>
    Full,

    /// <summary>The disk is not in the player's inventory (placed, traded, in another jukebox).</summary>
    DiskNotHeld,

    NotASongDisk,

    /// <summary>The disk names a song the hotel does not have, so there is nothing to play.</summary>
    UnknownSong,

    NoSuchSlot,
}
