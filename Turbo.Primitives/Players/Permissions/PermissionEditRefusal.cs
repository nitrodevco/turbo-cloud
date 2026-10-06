namespace Turbo.Primitives.Players.Permissions;

/// <summary>Why <see cref="PermissionEditor"/> refuses a change, if it does.</summary>
public enum PermissionEditRefusal
{
    None,

    /// <summary>The editor does not hold <c>permissions.manage</c>.</summary>
    NeedsManageNode,

    /// <summary>The group, or the weight asked for, is not lighter than the editor's heaviest group.</summary>
    GroupTooHeavy,

    /// <summary>The player's heaviest group is not lighter than the editor's: themselves included.</summary>
    PlayerTooHeavy,

    /// <summary>The node, or a node the wildcard covers, is one the editor does not hold.</summary>
    NodeNotHeld,

    /// <summary>The group gives <c>permissions.superuser</c>, which only a holder may hand out.</summary>
    NeedsSuperuser,
}
