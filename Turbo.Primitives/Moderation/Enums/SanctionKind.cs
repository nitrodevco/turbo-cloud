namespace Turbo.Primitives.Moderation.Enums;

/// <summary>
/// What a row of <c>player_sanctions</c> does to a player. A silence and a trade lock are not
/// here: they are temporary denials of a permission node, which the permission grains already
/// expire and audit.
/// </summary>
public enum SanctionKind
{
    /// <summary>The player cannot log in.</summary>
    Ban = 1,
}
