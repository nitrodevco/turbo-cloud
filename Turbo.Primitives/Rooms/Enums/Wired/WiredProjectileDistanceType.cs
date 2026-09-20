namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>How far the projectile addon lets a move's animation fly, as the client's editor numbers the choice.</summary>
public enum WiredProjectileDistanceType
{
    /// <summary>From the start tile to the target tile.</summary>
    Normal = 0,

    /// <summary>Past the target by a number of tiles.</summary>
    Overshoot = 1,

    /// <summary>Always the same number of tiles, however near the target is.</summary>
    Fixed = 2,
}
