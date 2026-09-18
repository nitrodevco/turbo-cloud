using System;
using System.Globalization;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms;

/// <summary>
/// A wall item location as the client sends it: <c>:w=x,y l=offset,z r</c>, where the trailing
/// letter is <c>l</c> for the left wall (south) and <c>r</c> for the right wall (west).
/// </summary>
public readonly record struct WallPosition(
    int X,
    int Y,
    int WallOffset,
    Altitude Z,
    Rotation Rotation
)
{
    private const string WALL_PREFIX = ":w=";
    private const string LOCATION_PREFIX = "l=";
    private const string LEFT_WALL = "l";
    private const int SEGMENT_COUNT = 3;

    public static bool TryParse(string? value, out WallPosition position)
    {
        position = default;

        if (
            string.IsNullOrWhiteSpace(value)
            || !value.StartsWith(WALL_PREFIX, StringComparison.Ordinal)
        )
            return false;

        var segments = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (
            segments.Length != SEGMENT_COUNT
            || !segments[1].StartsWith(LOCATION_PREFIX, StringComparison.Ordinal)
        )
            return false;

        var coords = segments[0][WALL_PREFIX.Length..].Split(',');
        var location = segments[1][LOCATION_PREFIX.Length..].Split(',');

        if (
            coords.Length != 2
            || location.Length != 2
            || !int.TryParse(
                coords[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var x
            )
            || !int.TryParse(
                coords[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var y
            )
            || !int.TryParse(
                location[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var offset
            )
            || !double.TryParse(
                location[1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var z
            )
        )
            return false;

        var rotation = segments[2].Equals(LEFT_WALL, StringComparison.OrdinalIgnoreCase)
            ? Rotation.South
            : Rotation.West;

        position = new WallPosition(x, y, offset, z, rotation);

        return true;
    }
}
