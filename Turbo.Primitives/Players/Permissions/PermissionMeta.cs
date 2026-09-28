using System.Globalization;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>Reading typed values out of a player's resolved meta.</summary>
public static class PermissionMeta
{
    /// <summary>
    /// A limit from its meta value, or <paramref name="fallback"/> — the hotel's configured
    /// default — when nothing sets it or what is set is not a whole number of zero or more.
    /// </summary>
    public static int ReadLimit(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit)
        && limit >= 0
            ? limit
            : fallback;
}
