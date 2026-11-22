using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Import.Morningstar.Formats;

/// <summary>Morningstar's rotations are the client's 0-7, as Turbo's <see cref="Rotation"/> is.</summary>
internal static class Directions
{
    public static Rotation Rotation(int value) =>
        value is >= 0 and <= 7 ? (Rotation)value : Primitives.Rooms.Enums.Rotation.North;
}
