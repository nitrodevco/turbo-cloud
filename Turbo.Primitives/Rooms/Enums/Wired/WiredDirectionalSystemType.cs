namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// How the projectile addon turns "from here to there" into a direction, as the client's editor
/// numbers the choice. The editor explains each with a picture of the tiles around the shooter
/// (<c>wired_misc_directional_system_N</c>); these summaries were read from those pictures.
/// </summary>
public enum WiredDirectionalSystemType
{
    /// <summary>Straight along an axis only when exactly on it; every other tile is a diagonal.</summary>
    EightStraight = 0,

    /// <summary>Eight even wedges: along an axis within 22.5 degrees of it, diagonal otherwise.</summary>
    EightDiffuse = 1,

    /// <summary>The longer axis wins; a tile exactly on a diagonal counts as north or south.</summary>
    FourPreferVertical = 2,

    /// <summary>The longer axis wins; a tile exactly on a diagonal counts as east or west.</summary>
    FourPreferHorizontal = 3,
}
