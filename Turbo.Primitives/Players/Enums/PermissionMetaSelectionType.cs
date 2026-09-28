namespace Turbo.Primitives.Players.Enums;

/// <summary>
/// How a meta key's value is chosen when the player and several of their groups set it.
/// Registered with the key.
/// </summary>
public enum PermissionMetaSelectionType
{
    /// <summary>The first source in resolution order: the player, then groups by weight.</summary>
    Inheritance = 0,

    /// <summary>The largest numeric value any live source sets; for limits a group raises.</summary>
    HighestNumber = 1,

    /// <summary>The smallest numeric value any live source sets.</summary>
    LowestNumber = 2,
}
