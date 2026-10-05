namespace Turbo.Primitives.Networking.Extensions;

/// <summary>
/// The header space a plugin may use for the packets of a protocol extension of its own. A header
/// is a signed 16-bit value on the wire, so the space is 0-32767. Habbo's own ids stop near 4100
/// and grow upward, so <see cref="LAST_RESERVED_CORE_HEADER"/> leaves about five times that headroom
/// below the plugin space, which sits at the top of the wire range, and
/// Turbo keeps <see cref="FIRST_TURBO_HEADER"/>-<see cref="LAST_TURBO_HEADER"/> for its own
/// extensions. Everything else above the core space is open to plugins.
/// </summary>
public static class ExtensionPackets
{
    /// <summary>The last header kept for Habbo's protocol and core packets.</summary>
    public const int LAST_RESERVED_CORE_HEADER = 19999;

    /// <summary>The first header a plugin may register.</summary>
    public const int FIRST_PLUGIN_HEADER = 20000;

    /// <summary>The last header a plugin may register: the top of the signed 16-bit wire header.</summary>
    public const int LAST_PLUGIN_HEADER = short.MaxValue;

    /// <summary>The first header kept for Turbo's own protocol extensions.</summary>
    public const int FIRST_TURBO_HEADER = 30000;

    /// <summary>The last header kept for Turbo's own protocol extensions.</summary>
    public const int LAST_TURBO_HEADER = 30099;

    /// <summary>Whether <paramref name="header"/> is one a plugin may register.</summary>
    public static bool IsPluginHeader(int header) =>
        header is >= FIRST_PLUGIN_HEADER and <= LAST_PLUGIN_HEADER
        && header is < FIRST_TURBO_HEADER or > LAST_TURBO_HEADER;
}
