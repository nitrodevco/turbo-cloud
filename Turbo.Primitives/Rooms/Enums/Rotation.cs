using System;

namespace Turbo.Primitives.Rooms.Enums;

public enum Rotation
{
    None = -1,
    North = 0,
    NorthEast = 1,
    East = 2,
    SouthEast = 3,
    South = 4,
    SouthWest = 5,
    West = 6,
    NorthWest = 7,
}

public static class RotationExtensions
{
    public static readonly Rotation[] CARDINAL =
    {
        Rotation.North,
        Rotation.East,
        Rotation.South,
        Rotation.West,
    };

    public static Rotation FromDelta(int dx, int dy)
    {
        dx = Math.Clamp(dx, -1, 1);
        dy = Math.Clamp(dy, -1, 1);

        if (dx == 0 && dy == 0)
            throw new ArgumentException("Zero delta has no direction.");

        if (dx == 0 && dy < 0)
            return Rotation.North;
        if (dx > 0 && dy < 0)
            return Rotation.NorthEast;
        if (dx > 0 && dy == 0)
            return Rotation.East;
        if (dx > 0 && dy > 0)
            return Rotation.SouthEast;
        if (dx == 0 && dy > 0)
            return Rotation.South;
        if (dx < 0 && dy > 0)
            return Rotation.SouthWest;
        if (dx < 0 && dy == 0)
            return Rotation.West;

        return Rotation.NorthWest;
    }

    public static Rotation FromPoints(int fromX, int fromY, int toX, int toY) =>
        FromDelta(toX - fromX, toY - fromY);

    public static Rotation ToSitRotation(this Rotation rot) => (int)rot % 2 > 0 ? rot - 1 : rot;

    public static Rotation Rotate(this Rotation rot, int delta) =>
        (Rotation)(((int)rot + delta + 8) % 8);
}
