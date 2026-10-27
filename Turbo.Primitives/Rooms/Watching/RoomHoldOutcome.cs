namespace Turbo.Primitives.Rooms.Watching;

/// <summary>What asking a room to stay loaded and awake did.</summary>
public enum RoomHoldOutcome
{
    /// <summary>Held (or renewed) until the lease runs out or it is released.</summary>
    Held,

    /// <summary>The room has as many holds as it takes (<see cref="RoomWatchLimits.MAX_HOLDS_PER_ROOM"/>).</summary>
    RoomFull,

    /// <summary>
    /// The plugin holds as many rooms on this server as it may
    /// (<see cref="RoomWatchLimits.MAX_ROOMS_PER_PLUGIN"/>); release one first.
    /// </summary>
    PluginFull,

    /// <summary>The room could not be loaded: there is none of that id, or it failed (logged).</summary>
    Failed,
}
