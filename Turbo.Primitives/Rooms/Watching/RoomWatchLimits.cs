using System;

namespace Turbo.Primitives.Rooms.Watching;

/// <summary>
/// How far plugins may keep rooms loaded and players watching them. A hold or watch ends on its
/// own when its lease runs out, so a plugin renews what it still needs; nothing keeps a room
/// loaded for good.
/// </summary>
public static class RoomWatchLimits
{
    /// <summary>The longest lease; a longer one is cut to this.</summary>
    public static readonly TimeSpan MAX_LEASE = TimeSpan.FromMinutes(10);

    /// <summary>The shortest lease; a shorter one is raised to this.</summary>
    public static readonly TimeSpan MIN_LEASE = TimeSpan.FromSeconds(1);

    /// <summary>Holds one room takes, plugin holds and watchers' holds together.</summary>
    public const int MAX_HOLDS_PER_ROOM = 256;

    /// <summary>Rooms one plugin may hold or have watched at once, on each server.</summary>
    public const int MAX_ROOMS_PER_PLUGIN = 100;

    /// <summary>Rooms one player may watch at once.</summary>
    public const int MAX_WATCHES_PER_PLAYER = 4;

    /// <summary><paramref name="lease"/> within <see cref="MIN_LEASE"/> and <see cref="MAX_LEASE"/>.</summary>
    public static TimeSpan Clamp(TimeSpan lease) =>
        lease < MIN_LEASE ? MIN_LEASE
        : lease > MAX_LEASE ? MAX_LEASE
        : lease;
}
