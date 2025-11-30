using System;

namespace Turbo.Contracts.Enums.Rooms.Object;

public static class RotationExtensions
{
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
}
