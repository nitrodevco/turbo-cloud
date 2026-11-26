namespace Turbo.Primitives.Players.Enums;

/// <summary>
/// What asking to preview a group came to (<c>docs/permissions.md</c>, §9, <i>Previewing a
/// group</i>).
/// </summary>
public enum PermissionPreviewResultType
{
    /// <summary>The player now holds what a plain member of the group holds, until the preview ends.</summary>
    Started = 0,

    /// <summary>No group has that name.</summary>
    UnknownGroup = 1,

    /// <summary>The group is not lighter than the player's own heaviest group.</summary>
    GroupTooHeavy = 2,

    /// <summary>The group would give the player a node they do not hold themselves.</summary>
    NodeNotHeld = 3,
}
