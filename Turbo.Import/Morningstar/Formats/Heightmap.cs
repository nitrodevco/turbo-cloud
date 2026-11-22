using System;
using System.Linq;

namespace Turbo.Import.Morningstar.Formats;

/// <summary>
/// A room model's floor, as both emulators write it: a row of tiles a line, <c>x</c> for no tile,
/// <c>0</c>-<c>9</c> then <c>a</c>-<c>z</c> for heights 0 to 35. Morningstar separates rows with
/// <c>\r</c>, <c>\n</c> or both; Turbo reads any of them, so the rows are kept as they are and only
/// what Turbo would refuse is caught here.
/// </summary>
internal static class Heightmap
{
    /// <summary>
    /// The heightmap as Turbo stores it (rows separated by <c>\r\n</c>), or null when it holds a
    /// tile Turbo's model compiler refuses, or no tile at all.
    /// </summary>
    public static string? Clean(string? heightmap)
    {
        if (string.IsNullOrWhiteSpace(heightmap))
            return null;

        var rows = heightmap
            .Trim()
            .ToLowerInvariant()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        if (rows.Count == 0)
            return null;

        foreach (var row in rows)
        foreach (var tile in row)
            if (tile != 'x' && !char.IsAsciiDigit(tile) && tile is not (>= 'a' and <= 'z'))
                return null;

        return rows.Any(row => row.Any(tile => tile != 'x')) ? string.Join("\r\n", rows) : null;
    }
}
