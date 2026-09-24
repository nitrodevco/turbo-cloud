namespace Turbo.Primitives.Guilds.Enums;

/// <summary>
/// Which of the three palettes a colour row belongs to. The editor asks for them as three
/// separate lists, and a group picks one colour from each of the last two; the first is what a
/// badge part is tinted with.
/// </summary>
public enum GuildColorSlotType
{
    /// <summary>Tints a badge part.</summary>
    Badge = 0,

    /// <summary>The group's primary colour, which its furni recolours to.</summary>
    Primary = 1,

    /// <summary>The group's secondary colour.</summary>
    Secondary = 2,
}
